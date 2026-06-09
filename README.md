# PIX Studio Custom Activities Pack

Репозиторий содержит набор кастомных активностей для платформы **PIX Studio**, разработанных на C# (.NET 9).

## Содержание

### 1. Соединить две таблицы (`JoinDataTables`)
Аналог SQL Join для двух объектов `DataTable`
- **Типы соединений:** Inner, Left, Right, Full.
- **Поддержка ключей:** Одиночные и составные ключи (по нескольким колонкам).
- **Гибкий вывод:** Сохранение в новую таблицу, либо перезапись левой/правой таблицы.
- **TODO:** Начиная с .NET 10 в LINQ появились специализированные методы `LeftJoin` и `RightJoin`, нужно будет обновить логику при переходе на эту версию и старше.

## Требования для разработки
- .NET SDK (версия, соответствующая вашей версии PIX SDK, обычно .NET 6 или .NET 8)
- IDE: VS Code / Visual Studio 2022 / JetBrains Rider

## Как использовать локально
1. Склонируйте репозиторий:
```bash
git clone [https://github.com/Yvunglord/pix-activities.git](https://github.com/Yvunglord/pix-activities.git)