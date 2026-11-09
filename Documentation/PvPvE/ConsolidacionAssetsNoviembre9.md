# Consolidación de Generated — 9 de noviembre de 2026

## Resultado

- 1.170 assets inventariados, aproximadamente 1,08 GB, sin contar `.meta`.
- 456 assets trasladados mediante `AssetDatabase.MoveAsset`, conservando sus GUID y metadatos.
- 714 assets retirados: 712 sin dependencias externas/activas confirmadas y dos copias idénticas consolidadas.
- `Assets/Generated` y su `.meta` eliminados completamente. No quedan rutas físicas activas hacia esa carpeta ni referencias a los GUID retirados.
- Todo permanece local en `Develop_branch`, sin commit ni push. El proyecto copia no fue modificado.

## Recursos compartidos y variantes

Los clips `BurstRifle_FP_BoltFire.anim` y `BurstRifle_TP_BoltFire.anim` de NewWeapons pasaron a reutilizar las copias equivalentes de UnifiedWeapons. Se comprobaron contenido e importación, y se actualizaron sus cuatro referenciadores.

Los Controllers y Override Controllers con igual nombre visible no resultaron necesariamente equivalentes. Se conservaron las diferencias legítimas de FP, TP, partes del arma, equipamiento, recarga, disparo y variantes utilizadas por las escenas de validación. No se forzó una unificación de comportamientos. Los nombres que colisionaban recibieron un identificador corto de contenido; el contenido serializado y los nombres internos de animación se conservaron.

## Ubicaciones definitivas

- Controllers: `Assets/Animations` y `Assets/Animations/CoopPresentation`.
- Overrides: `Assets/Animations/OverrideFirstPerson` y `Assets/Animations/OverrideThirdPerson`.
- Clips: `Assets/Animations/CoopPresentation` y `Assets/Animations/Rebound`.
- Fuentes FBX de animación: `Assets/Art/Animations/Character`.
- Prefabs: `Assets/Prefabs/Weapons`, `Assets/Prefabs/Weapons/ThirdPerson` y `Assets/Prefabs/UI`.
- Iconos: `Assets/Art/Sprites/Icons/Weapons`.
- Materiales: `Assets/Art/Materials/Weapons` y `Assets/Art/Materials/Attachments`.
- Catálogo de accesorios: `Assets/Data/AttachmentCatalog.asset`.

Se utilizaron carpetas existentes. Las herramientas antiguas de editor fueron adaptadas para localizar sus recursos por GUID mediante `Assets/Editor/WeaponAssetLocations.cs` y su mapa JSON. Los identificadores de compatibilidad `weapon-assets:` no son rutas de disco ni recrean Generated. Los localizadores de pruebas fueron actualizados. No se modificaron scripts de gameplay, estadísticas, materiales internos, interfaces ni lógica de ninguna modalidad.

## Verificación

- Compilación de Unity sin errores de C# detectados.
- Comparación de los objetos importados y sus IDs antes/después; se conservaron los clips, duración, frecuencia y eventos. Los registros internos de importación del editor se excluyeron de la comparación.
- Los 456 contenidos conservados siguen siendo idénticos a sus respaldos, salvo la sustitución de los dos GUID equivalentes.
- Comprobación de dependencias de 18.540 assets fuera de Generated: ningún candidato retirado resultó necesario. Sin scripts faltantes en los prefabs revisados.
- 55 pruebas EditMode aprobadas, incluidas topología de Animator, clips efectivos de las tres armas iniciales, importación Humanoid y avatar propio, accesorios, clases, equipos y reglas de PvPvE.
- Tres pruebas PlayMode aprobadas con renderizado: selector de granadas/curaciones y salida de lobby con/sin sala local conectada.
- Los ajustes auxiliares que Unity reserializó se restauraron a su estado anterior a esta tarea.

La consulta amplia de dependencias emitió avisos de lectura de NavMesh/LightingData antiguos fuera de Generated; esos archivos no fueron modificados ni reparados en esta etapa. No se realizaron partidas completas de Historia, 5 Rondas, Supervivencia ni una sesión Host + Client completa tras esta limpieza. Esa comprobación manual queda pendiente.

## Respaldo y seguimiento

No quedaron assets que debieran permanecer en Generated. Se mantienen variantes diferentes que no pueden consolidarse con seguridad sin cambiar comportamiento o las comparaciones históricas de pruebas.

Respaldo recuperable completo y manifiestos: `C:/Users/andre/Documents/Codex/2026-10-28/el-escenario-no-esta-completo-pero-5/work/generated-consolidation-noviembre9/`.

- `Before/Assets/Generated/`: copia íntegra anterior.
- `Plan.json`: cada movimiento, eliminación y sustitución de GUID.
- `Inventory.json`, `Reachability.json`: inventario y referencias.
- `BeforeUnity.json`, `Result.json`, `Dependencies.json`: comprobaciones de Unity.
- `EditTests.xml`, `PlayTests.xml`, `FinalVerification.json`: resultados finales.

No se avanzó con bugs pendientes ni mejoras de gameplay.
