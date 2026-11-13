# Лабораторная работа №4: Автоматизация тестирования (Unity Test Runner) в CI/CD

Студент: Гоман Глеб

Репозиторий: https://github.com/Henieknn/ARPO_1

**Индивидуальный вариант: №3** — механика Health (Assets/Scripts/Mechanics/Health.cs), тип PlayMode. Тест проверяет: `Die()` приводит HP к 0, `IsAlive == false`.

## Цель работы

Настроить автоматический запуск Unity-тестов (EditMode и PlayMode) в облачном пайплайне так, чтобы облачная компиляция игры и сборка Docker-образа выполнялись только при успешном прохождении всех тестов.

## Ход работы

### Шаг 1: Разделение кодовой базы (Создание Assembly Definitions)

Весь код проекта по умолчанию компилируется в одну общую сборку — тесты не могли «видеть» скрипты игры, не попадая в финальный билд. Разделил кодовую базу на изолированные модули через .asmdef:

- `Assets/Scripts/GameAssembly.asmdef` — основная сборка игры (ссылки на пакеты Unity.InputSystem и Unity.Cinemachine, которые использует код игры);
- `Assets/Tests/EditMode/Tests.asmdef` — тестовая сборка с `includePlatforms: ["Editor"]` (только EditMode), ссылки: UnityEngine.TestRunner, UnityEditor.TestRunner, GameAssembly, precompiled nunit.framework.dll, defineConstraints: UNITY_INCLUDE_TESTS;
- `Assets/Tests/PlayMode/PlayModeTests.asmdef` — тестовая сборка без includePlatforms (только PlayMode), те же ссылки на GameAssembly.

В инспекторе каждой тестовой сборки в разделе Assembly Definition References добавил ссылку на GameAssembly и нажал Apply — тесты получили возможность проверять код игры.

![Шаг 1](docs/40.jpg)

### Шаг 2: Написание автоматических тестов

Создал три теста:

1. **EditMode** (`Assets/Tests/EditMode/PlayerTests.cs`) — `EditMode_PlayerTag_IsCorrect`: открывает сцену SampleScene через EditorSceneManager, проверяет наличие объекта Player, корректность тега «Player» (FindWithTag ненадежен — тегом Player помечены и враги) и наличие обязательных компонентов PlayerController и Health.

2. **PlayMode** (`Assets/Tests/PlayMode/PlayerHealthTests.cs`) — `PlayMode_PlayerTakesDamage_HealthDecreases`: создает временный GameObject с реальным скриптом Health (maxHP=3, Awake активируется после SetActive(true)), имитирует урон через Health.Decrement() — после первого урона персонаж жив, после трёх — мертв.

3. **PlayMode, индивидуальный вариант №3** (`Assets/Tests/PlayMode/HealthDieTests.cs`) — `PlayMode_HealthDie_HPReachesZero_IsAliveFalse`: проверяемый сценарий — механика мгновенной смерти. Создается временный объект со скриптом Health (maxHP=3), персонаж жив до вызова Die(), после вызова Health.Die() HP приведено к нулю и IsAlive == false. Тест добавлен в существующую сборку PlayModeTests (asmdef со ссылкой на GameAssembly), отдельная сборка не требуется.

Локальная проверка через Test Runner (Unity CLI, -runTests): EditMode 1/1 passed, PlayMode 2/2 passed — все тесты зелёные.

![Шаг 2](docs/41.jpg)

### Шаг 3: Настройка автоматического запуска тестов в GitHub Actions

Добавил в `.github/workflows/main.yml` задачу `run_tests` (после sanity_check, перед сборкой и зеркалированием):

- кэш папки Library по hashFiles('Packages/packages-lock.json', 'Assets/**/*.asmdef') — ускоряет повторные прогоны;
- установка официального CLI GameCI v0.1.72;
- запуск тестов в контейнере unityci/editor: `game-ci test --docker --testPlatforms='playmode;editmode;COMBINE_RESULTS' --engineVersion=6000.3.10f1 --coverageEnabled=false`;
- CLI вызывается напрямую, а не через game-ci/unity-test-runner@v4: текущий экшен формирует флаг --no-coverageEnabled, которого CLI v0.1.72 не понимает (Unknown argument, exit code 1);
- Coverage отключен (--coverageEnabled=false): инструментирование кода роняет Unity 6 в контейнере (SIGSEGV, exit code 139);
- секреты лицензии UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD;
- результаты тестов сохраняются артефактом test-results (if: always() — даже при падении).

Гейт качества: задачи mirror_repo и build_docker_image переведены на `needs: run_tests` — при падении любого теста последующие задачи даже не запускаются, сломанный коммит автоматически блокируется.

Граф зависимостей пайплайна:

```
sanity_check -> run_tests -> { mirror_repo, build_docker_image }
```

![Шаг 3](docs/42.jpg)

### Шаг 4: Безопасное добавление лицензии Unity

Для работы Test Runner в облаке требуется авторизация Unity. Лицензия Unity Personal активирована в Unity Hub, файл Unity_lic.ulf создан. В репозитории созданы три секрета (Settings -> Secrets and variables -> Actions):

- `UNITY_LICENSE` — всё содержимое файла .ulf (многострочно, вместе с тегами Root);
- `UNITY_EMAIL` — email аккаунта Unity;
- `UNITY_PASSWORD` — пароль аккаунта Unity (вход по email и паролю, без 2FA — в headless-режиме второй фактор ввести невозможно).

![Шаг 4](docs/43.jpg)

### Шаг 5: Проверка результатов и отчет

- Во вкладке Actions задача run_tests зелёная, в логе видны результаты тестов EditMode и PlayMode, артефакт test-results содержит XML-отчёты.
- Папка Assets/Tests с тестами и .asmdef присутствует в GitHub-репозитории.
- Демонстрация защиты пайплайна: временно изменил логику теста (Assert.AreEqual(1, 2)), выполнил пуш — run_tests стал красным, задачи сборки и публикации не запустились. Вернул исправленный вариант.
- Обновлена таблица в README.md ссылкой на LR4.md.

![Шаг 5](docs/44.jpg)

## Контрольные вопросы

**1. В чем принципиальное различие между EditMode и PlayMode тестами в Unity?**

EditMode-тесты — легковесные: проверяют конфигурацию сцены, наличие объектов, тегов и настроек без запуска симуляции игры, выполняются мгновенно (методы помечены [Test]). PlayMode-тесты — интеграционные: запускают игру в фоновом режиме на доли секунды, имитируют физику, ввод и изменение состояния объектов (методы помечены [UnityTest] и возвращают IEnumerator).

**2. Какую роль играет файл Assembly Definition (.asmdef) при создании тестовой структуры проекта?**

.asmdef разделяет единую сборку Assembly-CSharp на изолированные модули: скрипты игры собираются в GameAssembly, а тестовые библиотеки — в отдельные сборки со ссылками на GameAssembly. Это решает две задачи: тесты получают доступ к коду игры (через Assembly Definition References), и сами тестовые библиотеки (nunit) не попадают в финальную сборку игры. Настройка includePlatforms определяет режим запуска: «Editor» — только EditMode, без него — только PlayMode.

**3. Почему в файле main.yml задача build_docker_image теперь зависит от needs: run_tests?**

Этап тестирования — главный фильтр качества в CI/CD: сборка игры не должна начинаться, если новый коммит ломает существующие механики. needs: run_tests делает сборку Docker-образа и зеркалирование условными: при падении любого теста эти задачи даже не запускаются — сломанный коммит автоматически блокируется и не доходит до публикации.

**4. Как настройка includePlatforms в тестовой сборке влияет на выбор режима запуска тестов?**

Режим запуска тестов определяется настройкой тестовой сборки: сборка с «includePlatforms»: [«Editor»] компилируется только в редакторе и запускается только в EditMode; сборка без этого параметра (универсальная) — только в PlayMode. Поэтому EditMode- и PlayMode-тесты размещают в отдельных папках с отдельными .asmdef — иначе Unity запускает тесты в неправильном режиме, и они падают.

## Выводы

В ходе работы настроил автоматическое тестирование Unity-проекта в CI/CD: разделил кодовую базу на изолированные сборки через Assembly Definitions, написал EditMode- и PlayMode-тесты (включая индивидуальный вариант №3 — проверка механики Health.Die()), интегрировал облачный запуск тестов через GameCI CLI с активацией лицензии Unity по секретам, и превратил тесты в гейт качества: сборка Docker-образа и зеркалирование выполняются только при успешном прохождении всех тестов.
