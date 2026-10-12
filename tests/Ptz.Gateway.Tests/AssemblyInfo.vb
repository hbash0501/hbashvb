Imports Xunit

' اختبارات التكامل تُنشئ خادماً كاملاً لكل اختبار وتنتظر بشكل متزامن؛ التوازي يجوّع مجمّع الخيوط.
<Assembly: CollectionBehavior(DisableTestParallelization:=True)>
