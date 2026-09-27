# Task 11 — QA: компиляция

Зависит от: task 1–10.

## Что проверять

### Компиляция
- [ ] Assembly-CSharp собирается без ошибок
- [ ] Ноль новых предупреждений Unity (warnings про неиспользуемые поля тоже считать)
- [ ] Нет `MissingReferenceException` при загрузке сцены

### DI-граф
- [ ] `LevelLifetimeScope` разрешается целиком при старте сцены. VContainer падает лениво —
      ошибка проявится при первом обращении к `CarService`, а не на `Awake`
- [ ] `EngineModel` / `TransmissionModel` / `DrivetrainModel` получают непустые SO
- [ ] `TransmissionService.ClutchMax` не равен нулю (иначе сцепление мертво и машина не едет)
- [ ] `EngineService.Inertia` не ноль (иначе деление на ноль в `UpdateAngularSpeed`)

### Значения из ассетов
- [ ] Ни одно поле SO не равно нулю там, где ноль ломает физику
- [ ] `Gears.Length >= 2`, `SteerAngleCurve` не `null`

### Состояния
- [ ] `DriveCarState` и `DriftCarState` не содержат дублей шинных расчётов
- [ ] `Time.fixedDeltaTime` не используется в стейтах
- [ ] `effMass` везде `BaseMass + EngineMass`

## Критерии приёмки
- [ ] Проект компилируется с нулём ошибок
- [ ] Сцена стартует без исключений в консоли
- [ ] Результаты зафиксированы в отчёте (что проверено, что найдено)
