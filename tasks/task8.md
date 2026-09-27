# Task 8 — Мигрировать потребителей на новый `TransmissionService`

Зависит от: task 3, 6.

## Проблема
Четыре файла держат `using Game.Car.Controller.CarPhysics.Transmission;` — старый неймспейс.
Они обращаются к API, которого в новом классе нет.

## Файлы

### 1. `Assets/Game/Car/Tachometr/Code/TachometerController.cs`
- `using Game.Car.Controller.CarPhysics.Transmission;` → `using Car.Controller.CarPhysics.Transmission;`
- Использует `RpmChanged` и `GearChanged` — появятся в task 3. Проверить, что подписи совпадают.
- Строки 40 и 51 — закомментированные, **не трогать**.

### 2. `Assets/Game/Car/Audio/FmodEngineSound.cs`
- Тот же неймспейс.
- `_transmission.RpmChanged += UpdateFmodRpm` / `-=` в `OnDestroy` — API совпадает.
- Параметр FMOD `"RPM"` — новое значение приходит в тех же единицах (об/мин), менять не надо.

### 3. `Assets/Game/Car/Souls/Services/SoulDrainService.cs`
- Тот же неймспейс.
- `_transmission.SelectedGear` — свойство есть и в новом классе, без изменений.
- Логика `if (gear <= 0) return;` сохраняется.

### 4. `Assets/Game/UI/scrits/SettingsUI.cs`
- Тот же неймспейс.
- `_transmissionService.IsAutoTransmission` — появится в task 3.
- Тумблер в настройках должен реально переключать режим коробки.

## Критерии приёмки
- [ ] Ни в одном из четырёх файлов нет старого неймспейса
- [ ] Тумблер «автомат» в настройках переключает режим, и это видно на машине
- [ ] Стрелка тахометра реагирует на обороты и не залипает на холостых
- [ ] FMOD-параметр `"RPM"` получает реальные обороты
- [ ] Расход душ зависит от передачи (как раньше)
- [ ] Отписка от событий в `OnDestroy` на месте — без утечек
- [ ] Закомментированные строки не тронуты
