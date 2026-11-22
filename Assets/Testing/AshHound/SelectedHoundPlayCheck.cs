#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class SelectedHoundPlayCheck : MonoBehaviour
{
    readonly List<string> checks=new List<string>();
    void Check(bool ok,string s){checks.Add((ok?"PASS ":"FAIL ")+s);File.WriteAllLines("Assets/Testing/AshHound/results.txt",checks);}
    void Place(playercontroler p,Vector3 at){p.transform.position=at;var b=p.GetComponent<Rigidbody2D>();b.position=at;b.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    IEnumerator Start()
    {
        Application.runInBackground=true;yield return new WaitForSeconds(.5f);
        var p=FindAnyObjectByType<playercontroler>();var hp=p.GetComponent<AshHealth>();
        var dogs=FindObjectsByType<AshHound>(FindObjectsSortMode.None);Check(dogs.Length==2,"Two selected hounds placed in village");foreach(var d in dogs)d.enabled=false;
        var dog=dogs[0];if(dog.transform.position.x>150)dog=dogs[1];var origin=dog.transform.position;dog.enabled=true;
        Place(p,origin+new Vector3(5,1.5f));hp.GrantInvulnerability(10);yield return new WaitForSeconds(.45f);Check(dog.transform.position.x>origin.x+.2f,"Detects and pursues heroine");
        Place(p,origin+new Vector3(30,1.5f));yield return new WaitForSeconds(1);Check(Mathf.Abs(dog.transform.position.x-origin.x)<.6f,"Returns to its post");
        dog.ResetDog();hp.RestoreHealth(4);Place(p,origin+new Vector3(-1.8f,1.5f));yield return new WaitForSeconds(1.6f);Check(hp.CurrentHealth<4,"Timed bite deals actual HP damage");
        dog.ResetDog();hp.RestoreHealth(4);Place(p,origin+new Vector3(-1.6f,3.5f));p.enabled=false;p.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
        yield return new WaitForSeconds(1.6f);Check(hp.CurrentHealth==4,"No bite damage above low hitbox");p.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Dynamic;p.enabled=true;
        dog.ResetDog();hp.GrantInvulnerability(10);Place(p,origin+new Vector3(-1.5f,1.5f));yield return new WaitForSeconds(.15f);p.GetComponent<SpriteRenderer>().flipX=false;int before=dog.Health;bool accepted=p.GetComponent<PlayerAttackAnimation>().TryAttack();yield return new WaitForSeconds(.7f);Check(accepted&&dog.Health<before,"Existing heroine sword damages dog hurtbox");
        yield return new WaitForSeconds(.5f);dog.TakeDamage(99,p.transform.position);yield return new WaitForSeconds(.65f);Check(dog.State=="Death"&&!dog.hurtbox.enabled&&!dog.bite.enabled,"Death uses authored ash frames and disables combat");yield return new WaitForSeconds(.65f);Check(!dog.GetComponent<SpriteRenderer>().enabled&&!dog.GetComponent<Rigidbody2D>().simulated,"Ash dissolution leaves no invisible body");
        AshHound.ResetAll();Check(dog.Health==3&&dog.hurtbox.enabled&&dog.GetComponent<SpriteRenderer>().enabled,"Lantern/respawn hook restores dog");
        foreach(var d in dogs)d.enabled=false;Place(p,origin+new Vector3(-3,1.5f));hp.RestoreHealth(4);
        foreach(var camera in FindObjectsByType<camerafollow>(FindObjectsSortMode.None))camera.target=p.transform;
        yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot("Assets/Testing/AshHound/in-game.png");yield return new WaitForEndOfFrame();yield return new WaitForSeconds(.15f);EditorApplication.isPlaying=false;
    }
}
#endif
