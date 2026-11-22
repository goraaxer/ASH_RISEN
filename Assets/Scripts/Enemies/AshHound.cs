using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(CapsuleCollider2D))]
public sealed class AshHound : MonoBehaviour
{
    public BoxCollider2D hurtbox, bite;
    public int maxHealth=3;
    public float speed=3.2f, detection=8, leash=12;
    public int Health {get;private set;}
    public string State {get;private set;}="Idle";
    public int JumpCount {get;private set;}
    Rigidbody2D body; Animator animator; SpriteRenderer visual; CapsuleCollider2D shape;
    playercontroler player;Vector3 post;float facing=1,stateAt,nextAttack,hurtUntil,nextJump;bool hit,returning;
    void Awake(){body=GetComponent<Rigidbody2D>();animator=GetComponent<Animator>();visual=GetComponent<SpriteRenderer>();shape=GetComponent<CapsuleCollider2D>();post=transform.position;Health=maxHealth;bite.enabled=false;}
    void Start(){Bind();Play("Idle");}
    void Bind()
    {
        player=FindAnyObjectByType<playercontroler>();
        if(player!=null)foreach(var c in player.GetComponentsInChildren<Collider2D>())Physics2D.IgnoreCollision(shape,c);
        foreach(var dog in FindObjectsByType<AshHound>(FindObjectsSortMode.None))if(dog!=this)Physics2D.IgnoreCollision(shape,dog.GetComponent<Collider2D>());
    }
    void Play(string state){if(State==state&&animator.GetCurrentAnimatorStateInfo(0).IsName(state))return;State=state;stateAt=Time.time;animator.Play(state,0,0);if(state=="Attack")CombatAudio.Play(CombatCue.HoundBite,.43f);else if(state=="Hurt")CombatAudio.Play(CombatCue.EnemyHurt,.42f);else if(state=="Death")CombatAudio.Play(CombatCue.EnemyDeath,.5f);}
    bool Solid(Collider2D c)=>c!=null&&!c.isTrigger&&c.attachedRigidbody!=body&&c.GetComponentInParent<playercontroler>()==null&&c.GetComponentInParent<AshHound>()==null;
    RaycastHit2D Cast(Vector2 p,Vector2 dir,float range){foreach(var h in Physics2D.RaycastAll(p,dir,range))if(Solid(h.collider))return h;return default;}
    bool Grounded=>Cast(new Vector2(shape.bounds.center.x,shape.bounds.min.y+.08f),Vector2.down,.22f).collider!=null;
    void Stop(){body.linearVelocity=new Vector2(0,body.linearVelocity.y);bite.enabled=false;}
    void Update()
    {
        if(PauseMenu.InputBlocked||SceneTransitionManager.IsBusy){Stop();return;}
        if(Health<=0){Stop();if(Time.time-stateAt>1.2f){visual.enabled=false;body.simulated=false;shape.enabled=false;}return;}
        if(player==null){Bind();return;}
        if(player.GetComponent<AshHealth>().IsDead){Stop();return;}
        if(Health<=0)return;
        if(Time.time<hurtUntil){bite.enabled=false;return;}
        if(State=="Attack")
        {
            float elapsed=Time.time-stateAt;
            bite.transform.localPosition=new Vector3(facing*1.05f,.48f);bite.enabled=elapsed>=.25f&&elapsed<.375f;
            if(elapsed>=.25f)body.linearVelocity=new Vector2(0,body.linearVelocity.y);
            if(bite.enabled&&!hit)
            {
                var victim=player.GetComponent<Collider2D>();var dash=player.GetComponent<PlayerDash>();
                if(bite.bounds.Intersects(victim.bounds)&&(dash==null||!dash.IsDashing)&&player.GetComponent<AshHealth>().TakeDamage(1))
                {hit=true;PlayerHitReaction.Apply(player.gameObject,transform.position,new Vector2(3,2));}
            }
            if(elapsed>=.65f){bite.enabled=false;nextAttack=Time.time+1.3f;Play("Idle");}return;
        }
        float dx=player.transform.position.x-transform.position.x;
        if(Mathf.Abs(transform.position.x-post.x)>leash||Mathf.Abs(dx)>detection*1.5f)returning=true;
        float destination=returning?post.x:player.transform.position.x;float delta=destination-transform.position.x;
        if(returning&&Mathf.Abs(delta)<.3f){returning=false;Stop();Play("Idle");return;}
        if(!returning&&(Mathf.Abs(dx)>detection||Mathf.Abs(player.transform.position.y-transform.position.y)>4)){Stop();Play("Idle");return;}
        facing=Mathf.Abs(delta)>.05f?Mathf.Sign(delta):facing;visual.flipX=facing<0;
        if(!returning&&Mathf.Abs(dx)<2.5f&&Mathf.Abs(player.transform.position.y-(transform.position.y+.7f))<1.6f&&Grounded)
        {
            Stop();if(Time.time>=nextAttack){hit=false;Play("Attack");bool safe=Cast((Vector2)transform.position+new Vector2(facing*2,.2f),Vector2.down,.8f).collider!=null;body.linearVelocity=new Vector2(safe?facing*Mathf.Clamp((Mathf.Abs(dx)-1.05f)/.25f,0,5):0,4);}return;
        }
        Move(facing);Play("Walk");
    }
    void Move(float dir)
    {
        if(!Grounded){body.linearVelocity=new Vector2(dir*speed,body.linearVelocity.y);return;}
        var b=shape.bounds;Vector2 toe=new Vector2(b.center.x+dir*(b.extents.x+.15f),b.min.y+.18f);
        bool wall=Cast(toe,Vector2.right*dir,.35f).collider!=null,floor=Cast(toe,Vector2.down,.6f).collider!=null;
        if(!wall&&floor){body.linearVelocity=new Vector2(dir*speed,body.linearVelocity.y);return;}
        if(Time.time>=nextJump)
        {
            const float height=1.45f;float gravity=Mathf.Abs(Physics2D.gravity.y*body.gravityScale),vy=Mathf.Sqrt(2*gravity*height);
            for(float d=1.6f;d<speed*2*vy/gravity;d+=.2f)
            {
                var landing=Cast(new Vector2(b.center.x+dir*d,b.min.y+height),Vector2.down,height+.6f);float t=d/speed;
                if(landing.collider==null||landing.point.y>b.min.y+height-.15f||landing.point.y>b.min.y+vy*t-.5f*gravity*t*t)continue;
                if(Cast(landing.point+new Vector2(dir*b.extents.x,.1f),Vector2.down,.3f).collider==null)continue;
                bool clear=true;Vector2 last=new Vector2(b.center.x,b.min.y+.08f);
                for(int i=1;i<=16&&clear;i++){float at=t*i/16;var p=new Vector2(b.center.x+dir*speed*at,b.min.y+.08f+vy*at-.5f*gravity*at*at);foreach(float y in new[]{.12f,.5f,.85f})if(Cast(last+new Vector2(dir*b.extents.x,y),(p-last).normalized,(p-last).magnitude).collider!=null)clear=false;last=p;}
                if(clear){body.linearVelocity=new Vector2(dir*speed,vy);nextJump=Time.time+.8f;JumpCount++;return;}
            }
        }
        Stop();
    }
    public bool TakeDamage(int amount,Vector3 source)
    {
        if(Health<=0||Time.time<hurtUntil||amount<=0)return false;
        Health=Mathf.Max(0,Health-amount);bite.enabled=false;hurtUntil=Time.time+.18f;
        body.linearVelocity=new Vector2(Mathf.Sign(transform.position.x-source.x)*3,2);
        if(Health==0){hurtbox.enabled=false;body.linearVelocity=Vector2.zero;Play("Death");}else Play("Hurt");return true;
    }
    public void ResetDog(){body.simulated=true;shape.enabled=true;hurtbox.enabled=true;visual.enabled=true;transform.position=post;body.position=post;body.linearVelocity=Vector2.zero;Health=maxHealth;hurtUntil=0;nextAttack=Time.time+.6f;returning=false;Play("Idle");}
    public static void ResetAll(){foreach(var dog in FindObjectsByType<AshHound>(FindObjectsSortMode.None))dog.ResetDog();}
    void OnDisable(){if(bite!=null)bite.enabled=false;}
}
