using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SelectedHoundSetup
{
    const string Root="Assets/Sprites/Enemies/AshHound/";
    public const string PrefabPath="Assets/Prefabs/Enemies/AshHound.prefab";
    [MenuItem("Tools/Ashrisen/Selected dog/Import selected sprite sheet")]
    public static void Import()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play Mode first");
        Directory.CreateDirectory(Root+"Frames");Directory.CreateDirectory("Assets/Animations/Enemies/AshHound");Directory.CreateDirectory("Assets/Prefabs/Enemies");
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(Root+"Source/SelectedHound.png"));
        string[] rows={"Idle","Walk","Attack","Hurt","Collapse","Dissolve"};
        // Technical sprite-sheet extraction. One constant 40px cell scale for every pose;
        // magenta is transparency, never part of the dog's palette.
        for(int row=0;row<6;row++)for(int col=0;col<6;col++)
        {
            var pixels=new Color[1600];int bottom=40;
            for(int y=0;y<40;y++)for(int x=0;x<40;x++)
            {
                var c=source.GetPixel(Mathf.FloorToInt((col+(x+.5f)/40)*source.width/6),Mathf.FloorToInt((5-row+(y+.5f)/40)*source.height/6));
                if(c.r>.3f&&c.b>.3f&&c.r>c.g*1.8f&&c.b>c.g*1.8f)c=Color.clear;
                c.a=c.a>.5f?1:0;pixels[y*40+x]=c;if(c.a>0)bottom=Mathf.Min(bottom,y);
            }
            if(bottom==40&&!(row==5&&col==5))throw new Exception("Empty pose "+row+":"+col);
            var t=new Texture2D(64,64,TextureFormat.RGBA32,false);var final=new Color[4096];
            for(int y=bottom;y<40;y++)for(int x=0;x<40;x++)final[(y-bottom)*64+x+12]=pixels[y*40+x];
            t.SetPixels(final);t.Apply();string path=Root+"Frames/"+rows[row]+"_"+col+".png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
        }
        UnityEngine.Object.DestroyImmediate(source);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var path in Directory.GetFiles(Root+"Frames","*.png"))
        {
            var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=16;i.filterMode=FilterMode.Point;i.mipmapEnabled=false;i.textureCompression=TextureImporterCompression.Uncompressed;i.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();i.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0);settings.spriteMeshType=SpriteMeshType.FullRect;i.SetTextureSettings(settings);i.SaveAndReimport();
        }
        string anim="Assets/Animations/Enemies/AshHound/";var clips=new AnimationClip[5];
        for(int state=0;state<5;state++)
        {
            string name=state==4?"Death":rows[state];var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(anim+name+".anim");if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,anim+name+".anim");}clip.frameRate=state==0?6:state==1?10:12;
            int n=state==4?12:6;var keys=new ObjectReferenceKeyframe[n+1];for(int f=0;f<n;f++)keys[f]=new ObjectReferenceKeyframe{time=f/clip.frameRate,value=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Frames/"+(state==4?(f<6?"Collapse":"Dissolve"):name)+"_"+(f%6)+".png")};keys[n]=new ObjectReferenceKeyframe{time=n/clip.frameRate,value=keys[n-1].value};AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{type=typeof(SpriteRenderer),path="",propertyName="m_Sprite"},keys);var set=AnimationUtility.GetAnimationClipSettings(clip);set.loopTime=state<2;AnimationUtility.SetAnimationClipSettings(clip,set);EditorUtility.SetDirty(clip);clips[state]=clip;
        }
        string ctrlPath=anim+"AshHound.controller";var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);if(ctrl==null)ctrl=AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);var sm=ctrl.layers[0].stateMachine;foreach(var s in sm.states)sm.RemoveState(s.state);foreach(var clip in clips){var state=sm.AddState(clip.name);state.motion=clip;if(clip.name=="Idle")sm.defaultState=state;}EditorUtility.SetDirty(ctrl);
        var dog=new GameObject("Ash Hound",typeof(SpriteRenderer),typeof(Animator),typeof(Rigidbody2D),typeof(CapsuleCollider2D));
        dog.GetComponent<SpriteRenderer>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Frames/Idle_0.png");dog.GetComponent<SpriteRenderer>().sortingOrder=10;dog.GetComponent<Animator>().runtimeAnimatorController=ctrl;
        var body=dog.GetComponent<Rigidbody2D>();body.gravityScale=3;body.constraints=RigidbodyConstraints2D.FreezeRotation;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        var shape=dog.GetComponent<CapsuleCollider2D>();shape.direction=CapsuleDirection2D.Horizontal;shape.size=new Vector2(1.8f,.9f);shape.offset=new Vector2(0,.45f);
        var ai=dog.AddComponent<AshHound>();ai.hurtbox=Box(dog,"Body hurtbox",new Vector2(2.05f,1.05f),new Vector2(0,.525f));ai.bite=Box(dog,"Bite active window",new Vector2(1.1f,.55f),new Vector2(1.05f,.48f));ai.bite.enabled=false;
        PrefabUtility.SaveAsPrefabAsset(dog,PrefabPath);UnityEngine.Object.DestroyImmediate(dog);AssetDatabase.SaveAssets();
    }
    static BoxCollider2D Box(GameObject root,string name,Vector2 size,Vector2 position){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.localPosition=position;var c=g.AddComponent<BoxCollider2D>();c.size=size;c.isTrigger=true;return c;}
    [MenuItem("Tools/Ashrisen/Selected dog/Add to current village")]
    public static void Place()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Leave Play Mode");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="AshenVillage")throw new Exception("Open AshenVillage");
        Directory.CreateDirectory("Assets/Backups/SelectedHound");if(!File.Exists("Assets/Backups/SelectedHound/AshenVillage-before-dog.unity"))EditorSceneManager.SaveScene(scene,"Assets/Backups/SelectedHound/AshenVillage-before-dog.unity",true);
        var root=GameObject.Find("Selected Hound Encounters");if(root==null)root=new GameObject("Selected Hound Encounters");
        foreach(float x in new[]{110f,190f})
        {
            if(root.transform.Find("Ash hound "+x)!=null)continue;
            float ground=float.NegativeInfinity;foreach(var h in Physics2D.RaycastAll(new Vector2(x,12),Vector2.down,25))if(!h.collider.isTrigger&&h.collider.GetComponentInParent<playercontroler>()==null)ground=Mathf.Max(ground,h.point.y);
            if(float.IsNegativeInfinity(ground))throw new Exception("No ground at "+x);
            var dog=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));dog.name="Ash hound "+x;dog.transform.SetParent(root.transform);dog.transform.position=new Vector3(x,ground+.025f);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
}
