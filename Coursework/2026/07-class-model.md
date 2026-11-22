# Практична 7. Структура програми й класи

**Мета.** Визначити ключові класи, їх відповідальність і зв'язки.

```mermaid
classDiagram
  class playercontroler
  class PlayerDash
  class PlayerAttack
  class AshHealth
  class PlayerRespawn
  class GameSaveSystem
  class WorldDoor
  class BossArenaCombat
  class BossAttackHitbox
  class BossScarletMagic
  playercontroler --> PlayerDash : блокує рух під час ривка
  PlayerAttack --> BossArenaCombat : завдає шкоди
  BossAttackHitbox --> AshHealth : завдає шкоди
  PlayerRespawn --> AshHealth : відновлює
  GameSaveSystem --> PlayerRespawn : зберігає чекпойнт
  WorldDoor --> playercontroler : переносить
  BossArenaCombat --> BossAttackHitbox : відкриває вікно атаки
  BossArenaCombat --> BossScarletMagic : магія фази 2
```

**Поділ відповідальностей.** `playercontroler` керує переміщенням; `PlayerDash` — ривком; `PlayerAttack` — напрямом і часом удару; `AshHealth` — здоров'ям; `GameSaveSystem` — серіалізацією; `BossArenaCombat` — станами й фазами боса. Класи розміщено в `Assets/Scripts` і `Assets/AshHeartsHUD/Scripts`.

**Обмеження моделі.** Це оглядова діаграма, не автоматично згенерований повний UML усіх 88 C# файлів. Подальше розділення великого `BossArenaCombat` на менші компоненти може зменшити зв'язаність.
