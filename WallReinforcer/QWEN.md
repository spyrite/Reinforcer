# Reinforcer (WallReinforcer)

## Обзор проекта

**WallReinforcer** — это плагин для Autodesk Revit, предназначенный для автоматизации армирования строительных конструкций (стены, перекрытия, фундаменты, колонны, окна, двери и т.д.).

### Основные характеристики

- **Тип**: Библиотека классов (.NET Library) для Autodesk Revit
- **Автор**: Pavel Smirnov
- **Компания**: 1P
- **Описание**: Плагин для армирования конструкций 3D

## Поддерживаемые версии Revit

Проект поддерживает несколько версий Revit (2019–2025):

| Версия Revit | Target Framework |
|-------------|------------------|
| 2019–2020   | .NET Framework 4.7 |
| 2021–2024   | .NET Framework 4.8 |
| 2025        | .NET 8.0-windows |

## Технологии и зависимости

- **MahApps.Metro** (v2.4.11) — UI фреймворк для WPF
- **Prism.Core** (v8.1.97) — MVVM фреймворк
- **Revit.Async** (v2.1.1) — асинхронная работа с Revit API
- **Revit_All_Main_Versions_API_x64** — Revit API (версионно-зависимый)
- **Fody.PropertyChanged** — автоматическая генерация INotifyPropertyChanged

## Структура проекта

```
Reinforcer/
├── Reinforcer.slnx              # Решение (мультиверсионная конфигурация)
└── WallReinforcer/
    ├── WallReinforcer.csproj    # Основной проект
    ├── Revit/
    │   └── App.cs               # Точка входа плагина (IExternalApplication)
    └── Resources1P/             # Ресурсы локализации
        ├── RevitParameters.resx      # Параметры Revit (имена и GUID)
        ├── ElemMarkDescriptions.resx # Описания маркировок элементов
        ├── FamilyNamePrefixes.resx   # Префиксы имён семейств
        ├── IdenityZones.resx         # Зоны идентификации
        └── ReinforcementPartitionNames.resx # Имена разделов армирования
```

## Сборка и конфигурации

### Конфигурации сборки

Проект имеет конфигурации для каждой версии Revit в режимах Release и Debug:

- `2020`, `2021`, `2022`, `2023`, `2024`, `2025` — Release
- `Debug 2020`, `Debug 2021`, `Debug 2022`, `Debug 2023`, `Debug 2024`, `Debug 2025` — Debug

### Сборка

```bash
# Release для Revit 2025
dotnet build --configuration "2025"

# Debug для Revit 2025
dotnet build --configuration "Debug 2025"
```

### Запуск в режиме отладки

Debug конфигурации настроены на автоматический запуск Revit при отладке:
- Путь к Revit: `C:\Program Files\Autodesk\Revit {Version}\Revit.exe`
- Выходная папка: `bin\Debug\`

### Выходные файлы

- **Release**: `bin\Release\{RevitVersion}\`
- **Debug**: `bin\Debug\`

## Архитектура

### Точка входа

Класс `App` в `Revit/App.cs` реализует интерфейс `IExternalApplication`:

```csharp
public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application) { ... }
    public Result OnShutdown(UIControlledApplication application) { ... }
}
```

### Пространство имён

Корневое пространство имён: `RevitOSA.WallReinforcer`

### Ресурсы

Файлы `.resx` в папке `Resources1P/` содержат:
- Имена параметров Revit (с префиксом `1Пп_`)
- GUID параметров для привязки к элементам
- Описания маркировок и зон

## Соглашения разработки

- **Язык**: C# (latest version)
- **UI**: WPF с использованием MahApps.Metro
- **Архитектура**: MVVM (Prism)
- **Платформа**: AnyCPU (с использованием WPF)

### Условная компиляция

Проект использует директивы препроцессора для поддержки разных версий Revit:
- `REVIT2020`, `REVIT2021`, ..., `REVIT2025`
- `DEBUG`, `TRACE` (в Debug конфигурациях)

### Версионирование

В Debug режиме используется автоматическая генерация revision на основе времени суток:
```
.Dev.{Version}.{Revision}
```

## Примечания

- Проект использует `EnableDynamicLoading` для .NET Core (Revit 2025+)
- Отключено предупреждение `MSB3052` в Release конфигурации
- Для Revit 2025+ используется `net8.0-windows` вместо .NET Framework
