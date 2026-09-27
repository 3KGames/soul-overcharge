# Task 10 — Удалить старую систему

Зависит от: task 6, 8, 9.

## Проблема
После миграции потребителей старый код остаётся мёртвым, но всё ещё в проекте и в git.

## Файлы
- `Assets/Game/Car/Controller/CarPhysics/TransmissionOld/GearDataRpm.cs` + `.meta`
- `Assets/Game/Car/Controller/CarPhysics/TransmissionOld/TransmissionService.cs` + `.meta`
- папка `Assets/Game/Car/Controller/CarPhysics/TransmissionOld/` целиком + `.meta`
- `Assets/Game/Data/GearDataRpm.asset`

## Порядок
1. Убедиться, что на старый класс **больше нет ссылок ни в одном файле** — проверить поиском
   по `Game.Car.Controller.CarPhysics.Transmission`, `GearDataRpm`, `RpmChanged`, `GetAcceleration`.
2. Проверить, что `GearDataRpm.asset` не подключён ни в одной сцене и ни в одном префабе
   (иначе будет missing reference). Сцена `LevelScene.unity` — проверить обязательно.
3. Удалить файлы через Unity (правый клик → Delete), чтобы корректно удалились `.meta`.
4. Удалить `GearDataRpm.asset`.

## Важно
- Не коммитить. Пользователь не просил коммит.
- `git status` после удаления должен показывать удаления старых файлов без потерь в новых.

## Критерии приёмки
- [ ] Ноль ссылок на `GearDataRpm` и старый неймспейс `Game.Car.Controller.CarPhysics.Transmission`
- [ ] Папка `TransmissionOld` удалена вместе с `.meta`
- [ ] `GearDataRpm.asset` удалён
- [ ] В сценах и префабах нет missing reference на удалённый ассет
- [ ] Проект компилируется после удаления
