# Практичні роботи 2026 — Ashrisen

Це 20 індивідуальних звітів за темами файла `силабус 2026.docx`. Наскрізний проєкт — **Ashrisen**, 2D гра на Unity 6. Звіти описують стан проєкту, який лежить у корені цього репозиторію. Дата зрізу: 23.11.2026.

Звіти містять посилання на вихідний код, сцени та результати перевірок. Статус «підготовлено» означає, що документ створено; він не означає, що всі вимоги силабусу до зовнішніх сервісів уже виконано. Командні ролі, Figma, GitHub Issues, Pull Requests і презентацію перед аудиторією не задокументовано як виконані, якщо підтвердження немає.

| № | Тема | Звіт |
|---|---|---|
| 1 | Ідея та бачення | [01](01-project-vision.md) |
| 2 | Проблема й користувачі | [02](02-problem-and-users.md) |
| 3 | Життєвий цикл | [03](03-lifecycle.md) |
| 4 | Вимоги | [04](04-requirements.md) |
| 5 | User Stories | [05](05-user-stories.md) |
| 6 | Use Case | [06](06-use-cases.md) |
| 7 | Структура й класи | [07](07-class-model.md) |
| 8 | Інтерфейс | [08](08-interface.md) |
| 9 | Git та GitHub | [09](09-git-and-github.md) |
| 10 | Командний Git | [10](10-git-workflow.md) |
| 11 | Архітектура | [11](11-architecture.md) |
| 12 | MVP | [12](12-mvp.md) |
| 13 | Перша ітерація | [13](13-first-iteration.md) |
| 14 | Розробка функцій | [14](14-feature-iteration.md) |
| 15 | Ручне тестування | [15](15-manual-testing.md) |
| 16 | Автоматизовані перевірки | [16](16-automated-testing.md) |
| 17 | Планування | [17](17-project-management.md) |
| 18 | Дефекти | [18](18-defects.md) |
| 19 | Зміна вимог | [19](19-requirement-change.md) |
| 20 | Документація й демонстрація | [20](20-documentation-and-demo.md) |

## Запуск

Відкрити корінь репозиторію в Unity **6000.6.5f1**. У `File → Build Profiles` перевірити сцени з `ProjectSettings/EditorBuildSettings.asset`; початкова сцена — `Assets/Scenes/MainMenu.unity`. Папки `Library`, `Temp`, `Obj`, `Logs` генеруються локально й не входять у репозиторій.

## Перевірка

Збережені результати редакторських перевірок: [бос](Evidence/boss-dynamic-results.txt), [геометрія арени](Evidence/boss-arena-collision-results.txt), [Ash Hound](../../Assets/Testing/AshHound/results.txt). У звітах окремо вказано, що перевірено автоматично, а що потребує ручного проходження.
