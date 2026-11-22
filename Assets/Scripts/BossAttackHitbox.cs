using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossAttackHitbox : MonoBehaviour
{
    BossArenaCombat owner; BoxCollider2D box;
    readonly HashSet<AshHealth> attempted=new HashSet<AshHealth>();
    public int DamageCount { get; private set; }
    public int WindowCount { get; private set; }
    public bool WindowActive=>box!=null&&box.enabled;
    public void Configure(BossArenaCombat value){owner=value;box=GetComponent<BoxCollider2D>();box.isTrigger=true;box.enabled=false;}
    public void BeginSwing(){attempted.Clear();SetActiveWindow(false);}
    public void SetActiveWindow(bool value){if(box==null)box=GetComponent<BoxCollider2D>();if(owner==null)value=false;if(value){box.size=owner.CurrentHitboxSize;var o=owner.CurrentHitboxOffset;o.x*=owner.GetComponent<SpriteRenderer>().flipX?1:-1;transform.localPosition=o;}bool changed=box.enabled!=value;box.enabled=value;if(value&&changed){WindowCount++;if(owner.debugMode)Debug.Log("Boss claw window "+WindowCount+" bounds="+box.bounds,this);Physics2D.SyncTransforms();CheckOverlaps();}}
    void FixedUpdate(){if(WindowActive)CheckOverlaps();}
    void CheckOverlaps(){foreach(var c in Physics2D.OverlapBoxAll(box.bounds.center,box.bounds.size,0))Hit(c);}
    void OnTriggerEnter2D(Collider2D c){Hit(c);} void OnTriggerStay2D(Collider2D c){Hit(c);}
    void Hit(Collider2D c){if(!WindowActive||owner==null||owner.IsDefeated||!owner.IsAttacking||PauseMenu.InputBlocked||SceneTransitionManager.IsBusy)return;var p=c.GetComponentInParent<playercontroler>();if(p==null)return;var dash=p.GetComponent<PlayerDash>();if(dash!=null&&dash.IsDashing)return;var hp=p.GetComponent<AshHealth>();if(hp==null||!attempted.Add(hp))return;int before=hp.CurrentHealth;if(hp.TakeDamage(owner.attackDamage)){DamageCount++;hp.GrantInvulnerability(.7f);if(owner.debugMode)Debug.Log("Boss claw HP "+before+" -> "+hp.CurrentHealth,this);if(!hp.IsDead)PlayerHitReaction.Apply(p.gameObject,owner.transform.position,owner.knockbackForce);}else if(owner.debugMode)Debug.Log("Boss claw rejected by existing health/invulnerability",this);}
    void OnDrawGizmosSelected(){var c=GetComponent<BoxCollider2D>();Gizmos.color=c.enabled?Color.red:Color.yellow;Gizmos.matrix=transform.localToWorldMatrix;Gizmos.DrawWireCube(c.offset,c.size);}
    void OnDisable(){if(box!=null)box.enabled=false;}
}
