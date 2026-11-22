using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds deterministic pixel frames from the original 64x128 monster, with one fixed canvas/pivot.
public static class BossPixelAnimationBuilder
{
    const string Root = "Assets/sprites/boss/Dynamic";
    const int SourceWidth = 64, Width = 96, Height = 128;
    // Extra transparent width is needed for the original claws during wide arcs; art scale stays 16 PPU.
    static readonly Vector2 SourceBodyPivot = new Vector2(32, 57);
    static readonly Vector2 CanvasBodyPivot = new Vector2(48, 57);
    static readonly Vector2[] Pivots = {
        SourceBodyPivot, new Vector2(20, 61), new Vector2(26, 57), new Vector2(14, 50),
        new Vector2(51, 53), new Vector2(39, 44)
    };
    static readonly string[] PartNames = { "Torso", "Head", "NearForeleg", "FarForeleg", "NearHindleg", "FarHindleg" };
    static readonly int[] DrawOrder = { 5, 3, 0, 1, 4, 2 };

    struct Pose
    {
        public float bx, by, body, head, nearFore, farFore, nearHind, farHind;
        public static Pose Lerp(Pose a, Pose b, float t) => new Pose {
            bx=Mathf.Lerp(a.bx,b.bx,t), by=Mathf.Lerp(a.by,b.by,t), body=Mathf.Lerp(a.body,b.body,t),
            head=Mathf.Lerp(a.head,b.head,t), nearFore=Mathf.Lerp(a.nearFore,b.nearFore,t),
            farFore=Mathf.Lerp(a.farFore,b.farFore,t), nearHind=Mathf.Lerp(a.nearHind,b.nearHind,t),
            farHind=Mathf.Lerp(a.farHind,b.farHind,t)
        };
        public float Angle(int part) => part == 1 ? head : part == 2 ? nearFore : part == 3 ? farFore : part == 4 ? nearHind : part == 5 ? farHind : 0;
    }
    sealed class Key
    {
        public float at;
        public string label;
        public Pose pose;
        public Key(float at, string label, Pose pose) { this.at=at; this.label=label; this.pose=pose; }
    }
    sealed class Spec
    {
        public string name;
        public float duration;
        public bool loop;
        public Key[] keys;
        public Spec(string name, float duration, bool loop, params Key[] keys)
        { this.name=name; this.duration=duration; this.loop=loop; this.keys=keys; }
    }
    static Pose P(float bx=0,float by=0,float body=0,float head=0,float nf=0,float ff=0,float nh=0,float fh=0)
        => new Pose { bx=bx,by=by,body=body,head=head,nearFore=nf,farFore=ff,nearHind=nh,farHind=fh };
    static Key K(float at,string label,Pose pose) => new Key(at,label,pose);

    // Sparse, hand-timed poses: keep the anticipation readable, then cross the impact in a few frames.
    // Values are normalized clip time, so combat windows and total move duration stay in sync.
    static float[] FrameTimes(string name) => name switch
    {
        "Idle" => new[] { 0f,.18f,.28f,.48f,.58f,.82f },
        "Walk" => new[] { 0f,.12f,.25f,.38f,.5f,.62f,.75f,.88f },
        "Reposition" => new[] { 0f,.12f,.23f,.42f,.49f,.75f,.84f,1f },
        "RendingClaw" => new[] { 0f,.11f,.18f,.30f,.37f,.45f,.49f,.55f,.65f,.82f,1f },
        "DoubleClawCombo" => new[] { 0f,.12f,.20f,.25f,.33f,.38f,.46f,.52f,.58f,.68f,.81f,.91f,1f },
        "HuntersLeap" => new[] { 0f,.10f,.18f,.30f,.35f,.56f,.68f,.76f,.88f,1f },
        "HuntersLeapStrike" => new[] { 0f,.10f,.18f,.30f,.35f,.50f,.55f,.66f,.73f,.85f,1f },
        "AshenSweep" => new[] { 0f,.12f,.20f,.32f,.36f,.44f,.48f,.56f,.69f,.84f,1f },
        "FallenBell" => new[] { 0f,.12f,.20f,.31f,.38f,.48f,.54f,.59f,.70f,.82f,.92f,1f },
        "FalseOpening" => new[] { 0f,.20f,.34f,.48f,.59f,.63f,.70f,.75f,.84f,1f },
        "TrueOpening" => new[] { 0f,.20f,.34f,.55f,.75f,.88f,1f },
        "ClawLeapSlam" => new[] { 0f,.10f,.16f,.20f,.27f,.31f,.39f,.43f,.55f,.60f,.70f,.76f,.83f,.92f,1f },
        "AntiAirClaw" => new[] { 0f,.12f,.20f,.35f,.40f,.49f,.55f,.64f,.72f,1f },
        "CeilingDescent" => new[] { 0f,.10f,.18f,.30f,.36f,.47f,.55f,.61f,.72f,.79f,.87f,1f },
        "TripleAshWave" => new[] { 0f,.11f,.18f,.21f,.32f,.38f,.45f,.49f,.58f,.65f,.73f,.78f,.84f,.90f,.95f,1f },
        "LastToll" => new[] { 0f,.13f,.18f,.22f,.31f,.39f,.45f,.49f,.58f,.67f,.72f,.77f,.83f,.90f,.94f,1f },
        "PhaseTransition" => new[] { 0f,.18f,.27f,.34f,.40f,.46f,.55f,.65f,.80f,.90f,1f },
        "Stagger" => new[] { 0f,.12f,.24f,.42f,.60f,1f },
        "Flinch" => new[] { 0f,.23f,.48f,.75f,1f },
        "Death" => new[] { 0f,.10f,.18f,.30f,.40f,.52f,.62f,.68f,.75f,.82f,.88f,.93f,.97f,1f },
        "IntroAwakening" => new[] { 0f,.08f,.16f,.25f,.34f,.43f,.50f,.55f,.58f,.63f,.70f,.78f,.88f,.94f,1f },
        "WendigoIdle" => new[] { 0f,.18f,.32f,.52f,.7f,.84f,.92f },
        "WendigoWalk" => new[] { 0f,.12f,.25f,.38f,.5f,.62f,.75f,.88f },
        "WendigoReposition" => new[] { 0f,.14f,.26f,.5f,.72f,1f },
        "WendigoAntlerRush" => new[] { 0f,.12f,.24f,.33f,.39f,.49f,.59f,.69f,.78f,.9f,1f },
        "WendigoCrimsonSigil" => new[] { 0f,.1f,.2f,.3f,.38f,.5f,.62f,.72f,.84f,1f },
        "WendigoScarletRain" => new[] { 0f,.1f,.2f,.25f,.36f,.44f,.49f,.6f,.68f,.73f,.84f,1f },
        "WendigoAwakening" => new[] { 0f,.1f,.2f,.3f,.4f,.5f,.6f,.72f,.84f,.93f,1f },
        "WendigoStagger" => new[] { 0f,.12f,.24f,.42f,.6f,1f },
        "WendigoFlinch" => new[] { 0f,.23f,.48f,.75f,1f },
        "WendigoDeath" => new[] { 0f,.10f,.18f,.30f,.40f,.52f,.62f,.68f,.75f,.82f,.88f,.93f,.97f,1f },
        _ => throw new ArgumentOutOfRangeException(nameof(name),name,"Missing pose timing")
    };

    static IEnumerable<Spec> Specs()
    {
        yield return new Spec("Idle",1.2f,true,
            K(0,"Idle",P()),K(.28f,"BreathIn",P(by:1,head:2,nf:1,nh:-1)),
            K(.58f,"BreathOut",P(by:-1,head:-2,ff:1)),K(1,"Return",P()));
        yield return new Spec("Walk",.75f,true,
            K(0,"Idle",P()),K(.25f,"LeftStep",P(by:1,nf:10,ff:-9,nh:-7,fh:5,head:2)),
            K(.5f,"WeightShift",P(by:-1,body:-1)),K(.75f,"RightStep",P(by:1,nf:-9,ff:9,nh:-5,fh:-5,head:-2)),
            K(1,"Return",P()));
        yield return new Spec("Reposition",.55f,false,
            K(0,"Idle",P()),K(.23f,"Brace",P(bx:1,by:-1,head:-4,nf:8,ff:4,nh:-6)),
            K(.49f,"Backstep",P(bx:2,by:1,body:3,head:6,nf:13,ff:-5,nh:-12,fh:5)),
            K(.75f,"Land",P(bx:1,by:-1,nf:-4,nh:4)),K(1,"Recovery",P()));
        yield return new Spec("RendingClaw",.62f,false,
            K(0,"Idle",P()),K(.18f,"Anticipation",P(bx:1,by:-2,body:-2,head:-3,nf:20,ff:4,nh:-4)),
            K(.37f,"Windup",P(bx:1,by:-3,body:-4,head:-4,nf:47,ff:6,nh:-5)),
            K(.49f,"Impact",P(bx:-3,by:1,body:4,head:3,nf:-48,ff:-4,nh:-5)),
            K(.65f,"FollowThrough",P(bx:-2,body:2,head:2,nf:-31,ff:-5)),
            K(1,"Recovery",P()));
        yield return new Spec("DoubleClawCombo",.94f,false,
            K(0,"Idle",P()),K(.12f,"Anticipation",P(by:-2,head:-3,nf:35,ff:3)),
            K(.25f,"FirstImpact",P(bx:-2,body:3,head:3,nf:-43,ff:2)),
            K(.38f,"SecondWindup",P(by:-1,head:-3,nf:-4,ff:28)),
            K(.52f,"SecondImpact",P(bx:-2,body:-3,head:4,nf:9,ff:-30)),
            K(.68f,"FollowThrough",P(by:-1,nf:4,ff:-8)),K(1,"Recovery",P()));
        yield return new Spec("HuntersLeap",.92f,false,
            K(0,"Idle",P()),K(.18f,"Crouch",P(by:-3,body:-4,head:-7,nf:19,ff:9,nh:-12,fh:6)),
            K(.35f,"Launch",P(by:2,body:5,head:8,nf:19,ff:12,nh:-19,fh:-12)),
            K(.56f,"Flight",P(by:3,body:3,head:4,nf:11,ff:13,nh:-17,fh:-8)),
            K(.76f,"Landing",P(by:-2,body:-4,head:-4,nf:-9,ff:-4,nh:5)),
            K(1,"Recovery",P()));
        yield return new Spec("HuntersLeapStrike",.92f,false,
            K(0,"Idle",P()),K(.18f,"Crouch",P(by:-3,body:-4,head:-7,nf:19,ff:9,nh:-12,fh:6)),
            K(.35f,"Launch",P(by:2,body:5,head:8,nf:19,ff:12,nh:-19,fh:-12)),
            K(.55f,"ClawDown",P(by:3,body:-2,head:-5,nf:-22,ff:-7,nh:-12)),
            K(.73f,"LandingStrike",P(by:-2,body:-5,head:-6,nf:-25,ff:-10,nh:5)),
            K(1,"Recovery",P()));
        yield return new Spec("AshenSweep",1.0f,false,
            K(0,"Idle",P()),K(.2f,"Anticipation",P(by:-2,body:-4,head:-7,nf:8,ff:2,nh:-5)),
            K(.36f,"Windup",P(by:-3,body:-4,head:-4,nf:38,ff:4)),
            K(.48f,"FloorImpact",P(bx:-2,by:-3,body:2,head:4,nf:-43,ff:-3)),
            K(.69f,"FollowThrough",P(by:-3,head:3,nf:-25,ff:-5)),K(1,"Recovery",P()));
        yield return new Spec("FallenBell",1.12f,false,
            K(0,"Idle",P()),K(.2f,"Anticipation",P(by:-1,body:-5,head:-6,nf:18,ff:15,nh:-8)),
            K(.38f,"RaisedClaws",P(by:1,body:3,head:6,nf:49,ff:39,nh:-10,fh:-4)),
            K(.54f,"GroundImpact",P(by:-3,body:-4,head:-7,nf:-10,ff:-9,nh:4)),
            K(.7f,"FollowThrough",P(by:-3,head:-5,nf:-8,ff:-7)),
            K(1,"Recovery",P()));
        yield return new Spec("FalseOpening",.85f,false,
            K(0,"Idle",P()),K(.2f,"HeadDrop",P(by:-2,body:-3,head:-12,nf:5,ff:4)),
            K(.48f,"HeldOpening",P(by:-2,body:-3,head:-12,nf:5,ff:4)),
            K(.63f,"TelltaleWindup",P(by:-2,head:4,nf:20,ff:4)),
            K(.75f,"LungeImpact",P(bx:-1,body:4,head:8,nf:-28)),K(1,"Recovery",P()));
        yield return new Spec("TrueOpening",.85f,false,
            K(0,"Idle",P()),K(.2f,"HeadDrop",P(by:-2,body:-3,head:-12,nf:5,ff:4)),
            K(.55f,"HeldOpening",P(by:-2,body:-3,head:-12,nf:5,ff:4)),
            K(.75f,"SlowRise",P(by:-1,body:-1,head:-5,nf:3,ff:2)),K(1,"Recovery",P()));
        yield return new Spec("ClawLeapSlam",1.35f,false,
            K(0,"Idle",P()),K(.1f,"ClawWindup",P(by:-2,head:-5,nf:19,ff:4)),
            K(.2f,"ClawImpact",P(body:3,head:6,nf:-28)),
            K(.31f,"Backstep",P(bx:2,by:-1,head:-5,nf:6,nh:-8)),
            K(.43f,"LeapLaunch",P(by:2,body:5,head:7,nf:17,ff:12,nh:-18)),
            K(.6f,"Airborne",P(by:3,body:2,head:2,nf:12,ff:8,nh:-12)),
            K(.76f,"Slam",P(by:-3,body:-6,head:-7,nf:-18,ff:-9)),
            K(1,"Recovery",P()));
        yield return new Spec("AntiAirClaw",.76f,false,
            K(0,"Idle",P()),K(.2f,"LookUp",P(by:-1,body:3,head:11,nf:8,nh:-6)),
            K(.4f,"RaisedClaw",P(by:-1,body:5,head:13,nf:52,ff:3)),
            K(.55f,"UpperImpact",P(body:3,head:14,nf:90,ff:-2,nh:-7)),
            K(.72f,"FollowThrough",P(head:8,nf:52,ff:-3)),K(1,"Recovery",P()));
        yield return new Spec("CeilingDescent",1.25f,false,
            K(0,"Idle",P()),K(.18f,"Crouch",P(by:-3,body:-7,head:-9,nf:17,ff:12,nh:-13)),
            K(.36f,"Vault",P(by:4,body:6,head:9,nf:20,ff:18,nh:-17,fh:-11)),
            K(.61f,"Fall",P(by:3,body:-5,head:-8,nf:-12,ff:-9,nh:8)),
            K(.79f,"Slam",P(by:-3,body:-5,head:-6,nf:-11,ff:-10,nh:5)),
            K(1,"Recovery",P()));
        yield return new Spec("TripleAshWave",1.42f,false,
            K(0,"Idle",P()),K(.11f,"WindupOne",P(by:-2,head:-6,nf:17)),
            K(.21f,"WaveOne",P(by:-2,body:2,head:5,nf:-27)),
            K(.38f,"WindupTwo",P(by:-2,body:-3,head:-4,ff:12,nf:7)),
            K(.49f,"WaveTwo",P(by:-2,body:-2,head:6,ff:-11,nf:3)),
            K(.65f,"WindupThree",P(by:-2,head:-6,nf:21)),
            K(.78f,"WaveThree",P(by:-2,body:3,head:7,nf:-30)),K(1,"Recovery",P()));
        yield return new Spec("LastToll",1.75f,false,
            K(0,"Idle",P()),K(.13f,"FirstWindup",P(by:-2,head:-7,nf:32,ff:24)),
            K(.22f,"FirstBell",P(by:-3,body:-5,nf:-9,ff:-7)),
            K(.39f,"SecondWindup",P(by:-2,head:6,nf:38,ff:28)),
            K(.49f,"SecondBell",P(by:-3,body:-6,head:-6,nf:-11,ff:-9)),
            K(.67f,"ThirdWindup",P(by:-2,head:8,nf:45,ff:34)),
            K(.77f,"ThirdBell",P(by:-4,body:-7,head:-10,nf:-13,ff:-10)),
            K(.9f,"Collapse",P(by:-4,body:-7,head:-12,nf:-6,ff:-4)),K(1,"Recovery",P()));
        yield return new Spec("PhaseTransition",1.4f,false,
            K(0,"Idle",P()),K(.18f,"Crouch",P(by:-3,body:-6,head:-10,nf:12,ff:8,nh:-8)),
            K(.34f,"TremorLeft",P(bx:-1,by:-3,body:-8,head:-12,nf:14,ff:10)),
            K(.46f,"TremorRight",P(bx:1,by:-3,body:-5,head:-9,nf:11,ff:7)),
            K(.65f,"Rise",P(by:1,body:5,head:8,nf:6,ff:4)),K(1,"Recovery",P()));
        yield return new Spec("Stagger",.5f,false,
            K(0,"Idle",P()),K(.24f,"Impact",P(bx:2,by:-1,body:6,head:-12,nf:14,ff:-4,nh:-7)),
            K(.6f,"Brace",P(bx:1,by:-2,head:-8,nf:8,ff:-3)),K(1,"Recovery",P()));
        yield return new Spec("Flinch",.22f,false,
            K(0,"Idle",P()),K(.23f,"Impact",P(bx:2,by:-1,body:4,head:-7,nf:8,ff:-3)),
            K(.75f,"Brace",P(bx:1,by:-1,head:-3,nf:3)),K(1,"Recovery",P()));
        yield return new Spec("Death",1.6f,false,
            K(0,"Idle",P()),K(.18f,"LastStagger",P(by:-2,body:7,head:-13,nf:16,ff:-4)),
            K(.4f,"Collapse",P(by:-5,body:-8,head:-18,nf:-18,ff:-9,nh:7)),
            K(.68f,"AshBreak",P(by:-6,body:-9,head:-20,nf:-20,ff:-10,nh:8)),
            K(1,"Gone",P(by:-6,body:-9,head:-20,nf:-20,ff:-10,nh:8)));
        // The introduction uses the same original-pixel rig and pivot as the fight.
        // Its first and last poses are the exact idle pose, so there is no sprite jump.
        yield return new Spec("IntroAwakening",5f,false,
            K(0,"Idle",P()),
            K(.16f,"HeadLower",P(by:-2,body:-3,head:-11,nf:5,ff:3)),
            K(.34f,"Stillness",P(by:-2,body:-3,head:-11,nf:5,ff:3)),
            K(.50f,"Tremor",P(bx:1,by:-3,body:-6,head:-17,nf:12,ff:6,nh:-5)),
            K(.58f,"FirstLook",P(by:-1,body:3,head:7,nf:9,ff:3)),
            K(.70f,"FullRaise",P(by:1,body:4,head:13,nf:5,ff:4,nh:-3)),
            K(.78f,"WatchHero",P(by:1,body:4,head:13,nf:5,ff:4,nh:-3)),
            K(.94f,"Settle",P(head:3,nf:2)),
            K(1,"IdleReturn",P()));
        yield return new Spec("WendigoIdle",1.15f,true,
            K(0,"Crouched",P(by:-1,body:-3,head:4,nf:5,nh:-4)),
            K(.32f,"Breath",P(by:1,body:2,head:6,nf:8,ff:-3)),
            K(.7f,"Hold",P(by:-2,body:-4,head:1,nf:3,nh:-5)),
            K(1,"Crouched",P(by:-1,body:-3,head:4,nf:5,nh:-4)));
        yield return new Spec("WendigoWalk",.7f,true,
            K(0,"Crouched",P(by:-1,body:-3,head:4,nf:5)),
            K(.25f,"ProwlLeft",P(by:1,body:2,head:5,nf:15,ff:-12,nh:-9,fh:7)),
            K(.5f,"Weight",P(by:-2,body:-5,head:2)),
            K(.75f,"ProwlRight",P(by:1,body:2,head:5,nf:-12,ff:14,nh:8,fh:-8)),
            K(1,"Crouched",P(by:-1,body:-3,head:4,nf:5)));
        yield return new Spec("WendigoReposition",.55f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.26f,"Brace",P(bx:1,by:-3,body:-7,head:-3,nf:15,nh:-9)),
            K(.5f,"Spring",P(bx:3,by:1,body:3,head:8,nf:-9,ff:7,nh:-12)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoAntlerRush",.98f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.24f,"Anticipation",P(by:-4,body:-8,head:-12,nf:16,ff:9,nh:-12)),
            K(.33f,"AimHorns",P(by:-4,body:-10,head:-18,nf:21,ff:11,nh:-14)),
            K(.49f,"Charge",P(bx:-4,by:1,body:8,head:-18,nf:-20,ff:-8,nh:-17)),
            K(.69f,"Gore",P(bx:-4,by:-1,body:5,head:-16,nf:-29,ff:-11,nh:-10)),
            K(.9f,"Brake",P(by:-3,body:-5,head:-8,nf:8,nh:4)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoCrimsonSigil",1.3f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.2f,"Carve",P(by:-3,body:-7,head:-10,nf:28,ff:10)),
            K(.38f,"ClawToStone",P(by:-4,body:-9,head:-12,nf:-35,ff:-8)),
            K(.62f,"Channel",P(by:-4,body:-9,head:-12,nf:-35,ff:-8)),
            K(.84f,"Release",P(by:0,body:4,head:8,nf:-9,ff:10)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoScarletRain",1.55f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.2f,"LookUp",P(by:-2,body:5,head:16,nf:18,ff:16)),
            K(.36f,"FirstCast",P(by:1,body:7,head:18,nf:67,ff:27)),
            K(.6f,"SecondCast",P(by:1,body:5,head:16,nf:42,ff:65)),
            K(.84f,"LastCast",P(by:0,body:8,head:17,nf:70,ff:59)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoAwakening",1.4f,false,
            K(0,"Collapse",P(by:-4,body:-8,head:-14,nf:12,ff:8)),
            K(.3f,"Convulsion",P(bx:-1,by:-5,body:-12,head:-20,nf:20,ff:15)),
            K(.5f,"HornsRise",P(bx:1,by:-4,body:-7,head:-8,nf:11,ff:8)),
            K(.72f,"Unfurl",P(by:2,body:8,head:14,nf:21,ff:17)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoStagger",.5f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.24f,"Impact",P(bx:2,by:-2,body:7,head:-15,nf:15,ff:-4)),
            K(.6f,"Brace",P(bx:1,by:-3,head:-9,nf:8)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoFlinch",.22f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.23f,"Impact",P(bx:2,by:-2,body:4,head:-7,nf:8)),
            K(.75f,"Brace",P(bx:1,by:-2,head:-3,nf:3)),
            K(1,"Crouched",P(by:-1,body:-3,head:4)));
        yield return new Spec("WendigoDeath",1.6f,false,
            K(0,"Crouched",P(by:-1,body:-3,head:4)),
            K(.18f,"LastStagger",P(by:-2,body:7,head:-13,nf:16,ff:-4)),
            K(.4f,"Collapse",P(by:-5,body:-8,head:-18,nf:-18,ff:-9,nh:7)),
            K(.68f,"AshBreak",P(by:-6,body:-9,head:-20,nf:-20,ff:-10,nh:8)),
            K(1,"Gone",P(by:-6,body:-9,head:-20,nf:-20,ff:-10,nh:8)));
    }

    [MenuItem("Tools/Ashrisen/Boss/Build Pixel Animations")]
    public static void Build()
    {
        string partsPath = Root + "/Parts/Torso.png";
        if (!File.Exists(partsPath)) BossPixelRigBuilder.Extract();
        Directory.CreateDirectory(Root + "/Frames");
        Directory.CreateDirectory(Root + "/KeyPoses");
        Directory.CreateDirectory(Root + "/Sheets");
        Directory.CreateDirectory(Root + "/Clips");
        var partPixels = new Color32[PartNames.Length][];
        for (int i=0;i<PartNames.Length;i++)
        {
            var t=BossPixelRigBuilder.Load(Root+"/Parts/"+PartNames[i]+".png");
            partPixels[i]=t.GetPixels32();
            UnityEngine.Object.DestroyImmediate(t);
        }
        var source=BossPixelRigBuilder.Load("Assets/sprites/boss/bossmodel.png");
        var sourcePixels=source.GetPixels32();
        var palette=new HashSet<Color32>(sourcePixels.Where(c=>c.a!=0));
        var backfill=MakeBackfill(partPixels);
        BossPixelRigBuilder.SavePixels(Root+"/Parts/JointBackfill.png",backfill,SourceWidth,Height);
        var report=new List<string>();
        foreach(var spec in Specs())
        {
            var frames=new List<Color32[]>();
            float[] times=FrameTimes(spec.name);
            int count=times.Length;
            Directory.CreateDirectory(Root+"/Frames/"+spec.name);
            Directory.CreateDirectory(Root+"/KeyPoses/"+spec.name);
            RemoveObsoleteFrames(spec.name,count);
            int newColors=0, clipped=0, detached=0, specks=0, longestBridge=0, failedBridges=0;
            for(int i=0;i<count;i++)
            {
                float t=times[i];
                Pose pose=Evaluate(spec.keys,t);
                bool wendigo=spec.name.StartsWith("Wendigo",StringComparison.Ordinal);
                float growth=spec.name=="WendigoAwakening"?t:1f;
                if(wendigo)pose=StandOnHindLegs(pose,growth);
                var frame=Render(partPixels,backfill,pose,spec.name.EndsWith("Death",StringComparison.Ordinal)?t:0,i,wendigo,growth);
                if(!IsRest(pose))RemoveTinyIslands(frame);
                if(!spec.name.EndsWith("Death",StringComparison.Ordinal)||t<.52f)
                {
                    ConnectParts(frame,out int bridge,out int missed);
                    longestBridge=Mathf.Max(longestBridge,bridge);
                    failedBridges+=missed;
                }
                foreach(var color in frame)if(color.a!=0&&!palette.Contains(color))newColors++;
                clipped+=CountClipped(partPixels,pose);
                CountComponents(frame,out int large,out int tiny);
                detached=Mathf.Max(detached,large);
                specks+=tiny;
                frames.Add(frame);
                BossPixelRigBuilder.SavePixels(Root+"/Frames/"+spec.name+"/"+spec.name+"_"+i.ToString("00")+".png",frame,Width,Height);
            }
            foreach(var key in spec.keys)
                BossPixelRigBuilder.SavePixels(Root+"/KeyPoses/"+spec.name+"/"+key.label+".png",
                    Render(partPixels,backfill,
                        spec.name.StartsWith("Wendigo",StringComparison.Ordinal)
                            ? StandOnHindLegs(key.pose,spec.name=="WendigoAwakening"?key.at:1f)
                            : key.pose,0,0,
                        spec.name.StartsWith("Wendigo",StringComparison.Ordinal),
                        spec.name=="WendigoAwakening"?key.at:1f),Width,Height);
            var sheet=new Color32[count*Width*Height];
            for(int frame=0;frame<count;frame++)
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
                    sheet[y*count*Width+frame*Width+x]=frames[frame][y*Width+x];
            string sheetPath=Root+"/Sheets/"+spec.name+".png";
            BossPixelRigBuilder.SavePixels(sheetPath,sheet,count*Width,Height);
            report.Add(spec.name+": frames="+count+" duration="+spec.duration+"s paletteNew="+newColors+" offCanvas="+clipped+
                " maxLargePieces="+detached+" tinyFragments="+specks+
                " longestBridge="+longestBridge+" missedBridges="+failedBridges);
        }
        UnityEngine.Object.DestroyImmediate(source);
        AssetDatabase.Refresh();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/sprites/boss/Boss.controller");
        foreach(var spec in Specs())
        {
            int count=FrameTimes(spec.name).Length;
            string sheetPath=Root+"/Sheets/"+spec.name+".png";
            ConfigureSheet(sheetPath,spec.name,count);
            var sprites=AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                .OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            if(sprites.Length!=count)throw new InvalidOperationException(spec.name+" sliced "+sprites.Length+" sprites; expected "+count);
            var clip=BuildClip(spec,sprites);
            SetState(controller,"BossDyn_"+spec.name,clip);
        }
        ConfigureSourceImages();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Artifacts/BossDynamicWorking");
        File.WriteAllLines("Artifacts/BossDynamicWorking/animation_quality.txt",report);
        Debug.Log("Built "+report.Count+" original-pixel boss clips. See Artifacts/BossDynamicWorking/animation_quality.txt");
    }

    static Pose Evaluate(Key[] keys,float t)
    {
        if(t<=keys[0].at)return keys[0].pose;
        for(int i=1;i<keys.Length;i++)if(t<=keys[i].at)
        {
            float u=Mathf.InverseLerp(keys[i-1].at,keys[i].at,t);
            return Pose.Lerp(keys[i-1].pose,keys[i].pose,u);
        }
        return keys[keys.Length-1].pose;
    }
    static Pose StandOnHindLegs(Pose pose,float amount)
    {
        // Rotate the original torso about its own pivot. Hind legs remain pointed at the
        // original ground line while forelegs become hanging arms; no body pixels are scaled.
        pose.by+=14f*amount;
        pose.body-=55f*amount;
        pose.head+=50f*amount;
        pose.nearFore+=55f*amount;
        pose.farFore+=55f*amount;
        pose.nearHind+=50f*amount;
        pose.farHind+=64f*amount;
        return pose;
    }
    static Color32[] MakeBackfill(Color32[][] parts)
    {
        var fill=new Color32[SourceWidth*Height];
        for(int part=1;part<parts.Length;part++)
        {
            Vector2 pivot=Pivots[part];
            for(int y=Mathf.Max(0,Mathf.RoundToInt(pivot.y)-7);y<Mathf.Min(Height,Mathf.RoundToInt(pivot.y)+8);y++)
            for(int x=Mathf.Max(0,Mathf.RoundToInt(pivot.x)-7);x<Mathf.Min(SourceWidth,Mathf.RoundToInt(pivot.x)+8);x++)
            {
                int idx=y*SourceWidth+x;
                if(parts[part][idx].a==0||parts[0][idx].a!=0)continue;
                int best=1000;Color32 chosen=default;
                for(int sy=Mathf.Max(0,y-7);sy<Mathf.Min(Height,y+8);sy++)
                for(int sx=Mathf.Max(0,x-7);sx<Mathf.Min(SourceWidth,x+8);sx++)
                {
                    int d=(x-sx)*(x-sx)+(y-sy)*(y-sy);
                    Color32 candidate=parts[0][sy*SourceWidth+sx];
                    if(candidate.a!=0&&d<best){best=d;chosen=candidate;}
                }
                if(best<=49)fill[idx]=chosen;
            }
        }
        return fill;
    }
    static Color32[] Render(Color32[][] parts,Color32[] backfill,Pose pose,float deathPhase,int frame,
        bool wendigo=false,float hornGrowth=1f)
    {
        var output=new Color32[Width*Height];
        if(!IsRest(pose))Draw(output,backfill,SourceBodyPivot,pose,0);
        foreach(int part in DrawOrder)Draw(output,parts[part],Pivots[part],pose,part);
        RepairJointPixels(output,pose);
        if(wendigo) DrawWendigo(output,pose,hornGrowth);
        if(deathPhase>.52f)
        {
            float amount=Mathf.InverseLerp(.52f,.97f,deathPhase);
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                int idx=y*Width+x;
                if(output[idx].a==0)continue;
                // A pixel only disappears once; changing the hash each frame made the outline flicker.
                uint hash=(uint)(x*73856093^y*19349663);
                if((hash%1000)/1000f<amount)output[idx]=default;
            }
        }
        return output;
    }
    static Color32[] antlers, backSpines;
    static void DrawWendigo(Color32[] output,Pose pose,float growth)
    {
        if(antlers==null)BuildWendigoDetails();
        if(growth>.6f)Draw(output,backSpines,SourceBodyPivot,pose,0);
        var visible=(Color32[])antlers.Clone();
        int upper=Mathf.RoundToInt(72+31*Mathf.Clamp01(growth));
        for(int y=0;y<Height;y++)for(int x=0;x<SourceWidth;x++)
            if(y>upper||(growth<.5f&&y<65))visible[y*SourceWidth+x]=default;
        Draw(output,visible,Pivots[1],pose,1);
    }
    static void BuildWendigoDetails()
    {
        antlers=new Color32[SourceWidth*Height];
        backSpines=new Color32[SourceWidth*Height];
        var shadow=new Color32(44,40,41,255);
        var bone=new Color32(121,111,101,255);
        var tip=new Color32(174,155,132,255);
        // Branches are drawn in original 64x128 source space and follow the head pivot.
        Line(antlers,16,72,12,80,shadow,2); Line(antlers,12,80,8,90,shadow,2);
        Line(antlers,8,90,5,101,shadow,2); Line(antlers,11,83,18,91,shadow,1);
        Line(antlers,8,89,1,94,shadow,1); Line(antlers,25,73,31,81,shadow,2);
        Line(antlers,31,81,36,91,shadow,2); Line(antlers,36,91,40,103,shadow,2);
        Line(antlers,31,82,24,91,shadow,1); Line(antlers,36,90,47,96,shadow,1);
        Line(antlers,16,73,12,80,bone,1); Line(antlers,12,80,8,90,bone,1);
        Line(antlers,8,90,5,101,tip,1); Line(antlers,11,83,18,91,bone,1);
        Line(antlers,8,89,1,94,tip,1); Line(antlers,25,74,31,81,bone,1);
        Line(antlers,31,81,36,91,bone,1); Line(antlers,36,91,40,103,tip,1);
        Line(antlers,31,82,24,91,tip,1); Line(antlers,36,90,47,96,tip,1);
        SetPixel(antlers,10,56,new Color32(164,57,49,255));
        SetPixel(antlers,11,56,new Color32(203,91,67,255));
        Line(backSpines,36,75,33,84,shadow,2); Line(backSpines,42,75,42,85,shadow,2);
        Line(backSpines,49,69,53,79,shadow,2);
        Line(backSpines,36,76,33,84,bone,1); Line(backSpines,42,76,42,85,bone,1);
        Line(backSpines,49,70,53,79,bone,1);
    }
    static void SetPixel(Color32[] pixels,int x,int y,Color32 color)
    {
        if(x>=0&&x<SourceWidth&&y>=0&&y<Height)pixels[y*SourceWidth+x]=color;
    }
    static void Line(Color32[] pixels,int x0,int y0,int x1,int y1,Color32 color,int thickness)
    {
        int dx=Math.Abs(x1-x0),dy=Math.Abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,err=dx-dy;
        while(true)
        {
            for(int ox=0;ox<thickness;ox++)for(int oy=0;oy<thickness;oy++)
                SetPixel(pixels,x0+ox,y0+oy,color);
            if(x0==x1&&y0==y1)break;
            int e2=2*err;
            if(e2>-dy){err-=dy;x0+=sx;}
            if(e2<dx){err+=dx;y0+=sy;}
        }
    }
    static bool IsRest(Pose p) => p.bx==0 && p.by==0 && p.body==0 && p.head==0 &&
        p.nearFore==0 && p.farFore==0 && p.nearHind==0 && p.farHind==0;
    static void RepairJointPixels(Color32[] output,Pose pose)
    {
        if(IsRest(pose))return;
        var repaired=(Color32[])output.Clone();
        for(int part=1;part<Pivots.Length;part++)
        {
            Vector2 center=CanvasBodyPivot+new Vector2(pose.bx,pose.by)+
                Rotate(Pivots[part]-SourceBodyPivot,pose.body);
            int cx=Mathf.RoundToInt(center.x),cy=Mathf.RoundToInt(center.y);
            for(int y=Mathf.Max(1,cy-3);y<=Mathf.Min(Height-2,cy+3);y++)
            for(int x=Mathf.Max(1,cx-3);x<=Mathf.Min(Width-2,cx+3);x++)
            {
                int idx=y*Width+x;
                if(output[idx].a!=0)continue;
                Color32 left=output[idx-1],right=output[idx+1],up=output[idx+Width],down=output[idx-Width];
                int neighbors=(left.a!=0?1:0)+(right.a!=0?1:0)+(up.a!=0?1:0)+(down.a!=0?1:0);
                if(neighbors>=3)repaired[idx]=left.a!=0?left:right.a!=0?right:up.a!=0?up:down;
            }
        }
        Array.Copy(repaired,output,output.Length);
    }
    static void CountComponents(Color32[] frame,out int large,out int tiny)
    {
        large=tiny=0;
        var visited=new bool[frame.Length];
        var queue=new int[frame.Length];
        for(int start=0;start<frame.Length;start++)
        {
            if(frame[start].a==0||visited[start])continue;
            int head=0,tail=0;queue[tail++]=start;visited[start]=true;
            while(head<tail)
            {
                int index=queue[head++],x=index%Width,y=index/Width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||nx>=Width||ny<0||ny>=Height)continue;
                    int neighbor=ny*Width+nx;
                    if(frame[neighbor].a==0||visited[neighbor])continue;
                    visited[neighbor]=true;queue[tail++]=neighbor;
                }
            }
            if(tail>=8)large++;else tiny++;
        }
    }
    static void RemoveTinyIslands(Color32[] frame)
    {
        var visited=new bool[frame.Length];
        var queue=new int[frame.Length];
        for(int start=0;start<frame.Length;start++)
        {
            if(frame[start].a==0||visited[start])continue;
            int head=0,tail=0;queue[tail++]=start;visited[start]=true;
            while(head<tail)
            {
                int index=queue[head++],x=index%Width,y=index/Width;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||nx>=Width||ny<0||ny>=Height)continue;
                    int neighbor=ny*Width+nx;
                    if(frame[neighbor].a==0||visited[neighbor])continue;
                    visited[neighbor]=true;queue[tail++]=neighbor;
                }
            }
            if(tail<8)for(int i=0;i<tail;i++)frame[queue[i]]=default;
        }
    }
    static void ConnectParts(Color32[] frame,out int longest,out int missed)
    {
        longest=missed=0;
        var visited=new bool[frame.Length];
        var queue=new int[frame.Length];
        var groups=new List<List<int>>();
        for(int start=0;start<frame.Length;start++)
        {
            if(frame[start].a==0||visited[start])continue;
            var group=new List<int>();
            int head=0,tail=0;queue[tail++]=start;visited[start]=true;
            while(head<tail)
            {
                int index=queue[head++],x=index%Width,y=index/Width;
                group.Add(index);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||nx>=Width||ny<0||ny>=Height)continue;
                    int neighbor=ny*Width+nx;
                    if(frame[neighbor].a==0||visited[neighbor])continue;
                    visited[neighbor]=true;queue[tail++]=neighbor;
                }
            }
            if(group.Count>=8)groups.Add(group);
        }
        if(groups.Count<2)return;
        groups.Sort((a,b)=>b.Count.CompareTo(a.Count));
        var main=new bool[frame.Length];
        foreach(int index in groups[0])main[index]=true;
        for(int i=1;i<groups.Count;i++)
        {
            int bestA=-1,bestB=-1,best=225;
            foreach(int index in groups[i])
            {
                int x=index%Width,y=index/Width;
                for(int dy=-12;dy<=12;dy++)for(int dx=-12;dx<=12;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||nx>=Width||ny<0||ny>=Height)continue;
                    int candidate=ny*Width+nx;
                    if(!main[candidate])continue;
                    int distance=dx*dx+dy*dy;
                    if(distance<best){best=distance;bestA=index;bestB=candidate;}
                }
            }
            if(bestA<0){missed++;continue;}
            longest=Mathf.Max(longest,Mathf.CeilToInt(Mathf.Sqrt(best)));
            Color32 color=frame[bestA];
            int x0=bestA%Width,y0=bestA/Width,x1=bestB%Width,y1=bestB/Width;
            int steps=Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(y1-y0));
            for(int step=0;step<=steps;step++)
            {
                int x=Mathf.RoundToInt(Mathf.Lerp(x0,x1,step/(float)Mathf.Max(1,steps)));
                int y=Mathf.RoundToInt(Mathf.Lerp(y0,y1,step/(float)Mathf.Max(1,steps)));
                int index=y*Width+x;
                if(frame[index].a==0)frame[index]=color;
                main[index]=true;
            }
            foreach(int index in groups[i])main[index]=true;
        }
    }
    static void Draw(Color32[] output,Color32[] source,Vector2 pivot,Pose pose,int part)
    {
        Vector2 position=CanvasBodyPivot+new Vector2(pose.bx,pose.by)+Rotate(pivot-SourceBodyPivot,pose.body);
        float angle=pose.body+pose.Angle(part);
        float radians=-angle*Mathf.Deg2Rad,c=Mathf.Cos(radians),s=Mathf.Sin(radians);
        for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
        {
            float dx=x+.5f-position.x,dy=y+.5f-position.y;
            int sx=Mathf.FloorToInt(pivot.x+dx*c-dy*s),sy=Mathf.FloorToInt(pivot.y+dx*s+dy*c);
            if(sx<0||sx>=SourceWidth||sy<0||sy>=Height)continue;
            Color32 color=source[sy*SourceWidth+sx];
            if(color.a!=0)output[y*Width+x]=color;
        }
    }
    static Vector2 Rotate(Vector2 point,float degrees)
    {
        float r=degrees*Mathf.Deg2Rad,c=Mathf.Cos(r),s=Mathf.Sin(r);
        return new Vector2(point.x*c-point.y*s,point.x*s+point.y*c);
    }
    static int CountClipped(Color32[][] parts,Pose pose)
    {
        int count=0;
        for(int part=0;part<parts.Length;part++)
        {
            Vector2 pivot=Pivots[part];
            Vector2 position=CanvasBodyPivot+new Vector2(pose.bx,pose.by)+Rotate(pivot-SourceBodyPivot,pose.body);
            float angle=pose.body+pose.Angle(part);
            for(int y=0;y<Height;y++)for(int x=0;x<SourceWidth;x++)
            {
                if(parts[part][y*SourceWidth+x].a==0)continue;
                Vector2 destination=position+Rotate(new Vector2(x+.5f-pivot.x,y+.5f-pivot.y),angle);
                if(destination.x<0||destination.x>=Width||destination.y<0||destination.y>=Height)count++;
            }
        }
        return count;
    }
    static void ConfigureSheet(string path,string name,int count)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=16;
        importer.maxTextureSize=8192;
        importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        var cells=new SpriteMetaData[count];
        for(int i=0;i<count;i++)cells[i]=new SpriteMetaData {
            name=name+"_"+i.ToString("00"),rect=new Rect(i*Width,0,Width,Height),
            alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(.5f,21f/Height)
        };
        importer.spritesheet=cells;
        importer.SaveAndReimport();
    }
    static void ConfigureSourceImages()
    {
        foreach(string folder in new[] { Root+"/Parts",Root+"/KeyPoses",Root+"/Frames" })
        foreach(string file in Directory.GetFiles(folder,"*.png",SearchOption.AllDirectories))
        {
            string path=file.Replace('\\','/');
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)continue;
            if(importer.textureType==TextureImporterType.Sprite && importer.spritePixelsPerUnit==16 &&
                importer.filterMode==FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression==TextureImporterCompression.Uncompressed)continue;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=16;
            importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;
            importer.SaveAndReimport();
        }
    }
    static void RemoveObsoleteFrames(string name,int count)
    {
        string folder=Root+"/Frames/"+name;
        foreach(string file in Directory.GetFiles(folder,name+"_*.png"))
        {
            string stem=Path.GetFileNameWithoutExtension(file);
            if(!int.TryParse(stem.Substring(name.Length+1),out int index)||index<count)continue;
            AssetDatabase.DeleteAsset(file.Replace('\\','/'));
        }
    }
    static AnimationClip BuildClip(Spec spec,Sprite[] sprites)
    {
        string path=Root+"/Clips/BossDyn_"+spec.name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){clip=new AnimationClip { name="BossDyn_"+spec.name };AssetDatabase.CreateAsset(clip,path);}
        clip.frameRate=24;
        clip.wrapMode=spec.loop?WrapMode.Loop:WrapMode.Once;
        float[] times=FrameTimes(spec.name);
        var keys=new ObjectReferenceKeyframe[sprites.Length+(spec.loop?1:0)];
        // Unity holds an object-reference key for one sample after its timestamp.
        // Place the final pose one editor frame early so clip.length matches the combat duration.
        float finalKeyTime=spec.duration-1f/clip.frameRate;
        for(int i=0;i<sprites.Length;i++)keys[i]=new ObjectReferenceKeyframe {
            time=!spec.loop&&i==sprites.Length-1?finalKeyTime:spec.duration*times[i],value=sprites[i]
        };
        if(spec.loop)keys[sprites.Length]=new ObjectReferenceKeyframe { time=finalKeyTime,value=sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding { path="",type=typeof(SpriteRenderer),propertyName="m_Sprite" },keys);
        var serialized=new SerializedObject(clip);
        var looping=serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if(looping!=null){looping.boolValue=spec.loop;serialized.ApplyModifiedProperties();}
        EditorUtility.SetDirty(clip);
        return clip;
    }
    static void SetState(AnimatorController controller,string name,AnimationClip clip)
    {
        var machine=controller.layers[0].stateMachine;
        AnimatorState state=null;
        foreach(var child in machine.states)if(child.state.name==name){state=child.state;break;}
        if(state==null)state=machine.AddState(name);
        state.motion=clip;
        state.speed=1;
        EditorUtility.SetDirty(state);
    }
}
