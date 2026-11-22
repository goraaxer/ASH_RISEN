using System;
using UnityEngine;
using UnityEngine.Events;

// Add this to the player. Movement and attack scripts remain separate.
[DisallowMultipleComponent]
public sealed class AshHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 4;
    [SerializeField, Min(0f)] private float damageCooldown = 0.5f;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    private int currentHealth;
    private bool initialized;
    private float nextDamageTime = float.NegativeInfinity;

    // Arguments: old HP, new HP, maximum HP.
    public event Action<int, int, int> HealthChanged;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth { get { Initialize(); return currentHealth; } }
    public bool IsDead => CurrentHealth == 0;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        currentHealth = MaxHealth;
        initialized = true;
    }

    public bool TakeDamage(int amount)
    {
        Initialize();
        if (amount <= 0 || IsDead || Time.time < nextDamageTime) return false;

        int old = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        nextDamageTime = Time.time + Mathf.Max(0f, damageCooldown);
        HealthChanged?.Invoke(old, currentHealth, MaxHealth);
        if (currentHealth == 0) onDeath.Invoke();
        return true;
    }

    public void GrantInvulnerability(float seconds) { nextDamageTime = Mathf.Max(nextDamageTime, Time.time + Mathf.Max(0, seconds)); }
    public void Heal(int amount)
    {
        Initialize();
        if (amount <= 0 || IsDead) return;
        int old = currentHealth;
        // Avoid overflow for unusually large healing values.
        currentHealth += Mathf.Min(amount, MaxHealth - currentHealth);
        if (old != currentHealth) HealthChanged?.Invoke(old, currentHealth, MaxHealth);
    }

    public void RestoreHealth(int value)
    {
        Initialize();
        int old = currentHealth;
        currentHealth = Mathf.Clamp(value, 1, MaxHealth);
        nextDamageTime = float.NegativeInfinity;
        HealthChanged?.Invoke(old, currentHealth, MaxHealth);
    }

    // Call explicitly when respawning. Disabling the player does not refill HP.
    public void ResetHealth()
    {
        Initialize();
        int old = currentHealth;
        currentHealth = MaxHealth;
        nextDamageTime = float.NegativeInfinity;
        HealthChanged?.Invoke(old, currentHealth, MaxHealth);
    }

    [ContextMenu("Test / Take 1 damage (Play Mode)")]
    private void TestDamage() { if (Application.isPlaying) TakeDamage(1); }

    [ContextMenu("Test / Heal 1 HP (Play Mode)")]
    private void TestHeal() { if (Application.isPlaying) Heal(1); }

    [ContextMenu("Test / Reset health (Play Mode)")]
    private void TestReset() { if (Application.isPlaying) ResetHealth(); }
}
