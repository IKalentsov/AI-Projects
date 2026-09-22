# 06: Приборка после ревью 04–05

**What to build:** Негативная проверка архитектурных правил вызывает проверяемый метод напрямую,
а не через Reflection; правило `AT-01` названо по измеряемой семантике. Поведение тестов не
меняется, число тестов не уменьшается.

Замечания — `reviews/05-fix-layer-tests_review.md`, замечания 1–3.

**Blocked by:** None (can start immediately)

**Скиллы:** просмотри доступные скиллы и примени один подходящий — конкретный не называю, выбери сам
(`spec.md` §3, «Скиллы»).

**Status:** ready-for-human — сдано; все критерии выполнены, оба необязательных пункта тоже

- [x] `AssertNoDependencyOn_throwsWhenForbiddenDependencyPresent` вызывает `AssertNoDependencyOn`
      **напрямую**: `Assert.Throws<Xunit.Sdk.XunitException>(() => AssertNoDependencyOn(fakeDeps, "FakeLayer", "WeatherBot.Domain"))`.
      Обвязка Reflection (`GetMethod`, `Invoke`, `BindingFlags`, `method!`, разворачивание
      `TargetInvocationException`) убрана; проверка сообщения исключения сохранена
- [x] `using System.Reflection` **остаётся**: в этом пространстве имён живут `Assembly` и
      `AssemblyName`, которыми файл пользуется. Требование тикета убрать его было ошибочным —
      исполнитель возразил по существу и был прав
- [x] канарейка дополнена второй парой: `Application → Domain` и `Infrastructure → Application`
- [x] `AT01_Domain_usesNoOtherProjectAssembly` переименован по семантике «использует»
- [x] Сборка: `Ошибок: 0`, `Предупреждений: 0`
- [x] `WeatherBot.UnitTests` — 122, `WeatherBot.ArchitectureTests` — 13, сбой `0`
- [x] `src/**`, `*.csproj`, `Directory.Packages.props` не изменены
- [x] Отчёт `reports/06-fix-layer-tests-cleanup_report.md`

## Comments

**2026-09-22, ревьюер.** Ревью — `reviews/06-fix-layer-tests-cleanup_review.md`. Вердикт:
**ОК, принято без доработок.** Мои прогоны: сборка `0/0`, `122` unit-теста (`684 ms`),
`13` архитектурных, сбой `0`; изменён ровно один файл — `LayerDependenciesTests.cs`.

Отдельно отмечено в ревью: исполнитель не выполнил ошибочную инструкцию тикета (убрать
`using System.Reflection`) и объяснил почему — это правильное поведение, ошибка была в тикете.
Замечания только к оформлению отчёта: вывод команд выглядит перенабранным (прямой слэш в пути,
нет строки `длительность`), а оставленный `using` не вынесен в раздел «Отклонения». Отдельного
тикета не требуют.
