# Task 14 — Вернуть `clutchError` в ассет

## Проблема

`Assets/Game/Data/Transmission.asset` содержит `clutchError: 0.1`, хотя дефолт в
`TransmissionSO` — `16.7`. Правка task 13 до ассета не доехала.

Из этого следует жёсткость сцепления `ClutchProportion = ClutchMax / 0.1 = 1207 Н*м на рад/с`
вместо расчётных `7.23`.

Петля интегрирования двигателя устойчива только при `dt < 2 * Inertia / ClutchProportion`:

```
Inertia      = 0.08 * 1.4        = 0.112 кг*м^2
ClutchMax    = 80.48 * 1.5       = 120.72 Н*м
dt (допустимый) = 2 * 0.112 / 1207 = 0.000186 с
dt (фактический) = 0.02 с          -> запас в 108 раз ПРОТИВ устойчивости
```

При этом момент упирается в потолок `±ClutchMax`, и один кадр потолка даёт двигателю
`120.72 / 0.112 * 0.02 = 21.6 рад/с = 206 об/мин`. Наблюдаемое в логе колебание
ровно ±200 об/мин с чередованием знака через кадр — предельный цикл насыщенного
сцепления, а не артефакт тахометра.

## Что сделать

1. В `Assets/Game/Data/Transmission.asset` выставить `clutchError: 16.7`.
2. Больше в ассете transmission ничего не менять. `gears` (6 значений) и `finalGear: 3.65`
   НЕ трогать — они текущие и выверенные.
3. Убедиться, что значение в ассете совпадает с дефолтом `TransmissionSO.clutchError`.

## Критерии приёмки

- `clutchError` в ассете равен `16.7`.
- `ClutchProportion` при дефолтах равен `120.72 / 16.7 = 7.23 Н*м на рад/с`.
- `dt < 2 * Inertia / ClutchProportion` выполняется: `0.031 с > 0.02 с`, запас ×1.55.
- `gears`, `finalGear`, `clutchCoef`, `efficiency`, `shiftUpRPM`, `shiftDownRPM`,
  `shiftCooldownTime`, `clutchEngageTime`, `launchSpeed` не изменены.
- Файл `Transmission.asset` парсится как валидный Unity YAML.
