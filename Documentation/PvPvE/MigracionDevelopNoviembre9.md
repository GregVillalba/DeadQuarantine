# Migración PvPvE a Develop_branch — 9 de noviembre de 2026

## Funcionalidades migradas

Se trasladó la implementación existente, sin añadir funcionalidades ni resolver incidencias pendientes: Team Deathmatch PvPvE, networking y matchmaking, lobby 3D, equipos, personajes, votación de los tres mapas, Create-a-Class, Pick 10, Wildcards, armas y accesorios, perks, curaciones, zombies, puntuación/asistencias, HUD/TAB/killfeed, carga, preparación/Ready, respawn, victoria/tiempo/overtime y regreso al lobby. También se incorporaron las extensiones compartidas ya existentes del origen.

Destino: `C:/Users/andre/Documents/GitHub/DeadQuarantine`, rama real `Develop_branch`. Inicialmente sin cambios locales.
Origen: `C:/Users/andre/Documents/GitHub/DeadQuarantine_PvPvE`, rama `feature/pvpve-team-deathmatch`. Utilizado sólo para lectura. Sus archivos incorporados se verificaron por hash y su estado de trabajo permanece igual al inicial.

## Archivos principales

- `Assets/Scripts/PvPvE/`, `Assets/PvPvE/`, `Assets/Resources/PvPvE/`: implementación, UI, configuración, escena y recursos del modo.
- `Assets/Generated/NewWeapons/` y presentaciones de `Assets/Generated/UnifiedWeapons/ThirdPerson/`: recursos y controladores existentes de armas/accesorios.
- `Assets/Prefabs/Player.prefab`: componentes y armas de PvPvE agregados sobre el prefab actualizado de develop.
- `Assets/Scenes/PantallasUI.unity`: entrada y configuración del modo combinadas con el menú actual.
- `Assets/Prefabs/Mercader.prefab`, scripts de jugador, armas, recursos, puntuación y tienda: estado compartido ya implementado en el origen.
- Scripts compartidos combinados: `PlayerHealth`, `PlayerMovement`, `HUDController`, `GrenadeThrow`, `PlayerSpectator`, `ZombieAI`, `ZombieHealth`, `NetworkBootstrap`, `MultiplayerPlayerSpawnAssigner`, `HostLeftPopup`, `RoundManager`, `PauseController`, `LoadGameMenuController`.
- `ProjectSettings/EditorBuildSettings.asset`: únicamente añadida `Assets/PvPvE/Scenes/PvPvEMatch.unity`, manteniendo todas las entradas de develop.
- Pruebas/herramientas existentes incorporadas desde el origen; añadido `Assets/Editor/PvPvE/PvPvEDevelopMigrationValidation.cs` exclusivamente para comprobar referencias, escenas y componentes.

Los GUID y `.meta` del origen se conservaron. No se trasladaron sus 64 eliminaciones de archivos. No se ejecutó limpieza ni reorganización.

## Conflictos y decisiones

Con la elección expresa del usuario se conservaron los cambios de sus compañeros y se incorporaron sólo los puntos necesarios de PvPvE:

- Conservados Historia, tutorial, minijuego, carga aditiva multiplayer, controles de fin de partida y protección de caídas/spawn de develop.
- Los cambios compatibles se integraron por bloques de código y por componentes de Unity, sin reemplazar escenas/prefabs completos.
- `PlayerHealth`: añadido el callback existente de muerte PvPvE dentro del flujo actualizado de eliminación de develop.
- `Player.prefab`: conservados ambos componentes añadidos por cada versión, sin descartar el de develop.
- Pausa: conservadas las condiciones coincidentes de configuración/reasignación de develop; incorporados los puntos del modo existentes.
- Tutorial y plantilla de escena conservados; no se trasladó el override antiguo del mercader del tutorial.
- Adaptación mínima en `PvPvETeamDeployment`: al completar el teletransporte por equipos se marca `IsAtSpawn`. Es necesario para integrarse con la protección de caídas reciente de develop; no cambia el flujo original de spawn.
- Paquetes, ajustes generales, render pipeline y registro global de prefabs conservados. El modo utiliza su registro de red propio existente. Se restauraron las reserializaciones auxiliares producidas por Unity durante las pruebas.

## Verificación realizada

- Compilación del editor en Unity 6000.5.8f1: correcta.
- 49 pruebas EditMode existentes: aprobadas, cero fallidas. Incluyen reglas, networking/serialización, votación, clases, catálogo y referencias de integración.
- Carga real de `PantallasUI`, `PvPvEMatch` y `MainSceneMultiPlayer`; 31 prefabs comprobados sin scripts faltantes, referencias principales de las 16 armas y tres mapas conectadas. Tres personajes con cinco variantes existentes.
- Conservación del gestor aditivo multiplayer de develop comprobada en su escena.
- Build Windows del destino: correcto.
- Host + Client locales reales (NGO/UTP): lobby, votación, carga sincronizada, selección de clase, equipos, comienzo de partida. Salida del último rival: victoria del Host, cierre de resultados, descarga del mapa y regreso al lobby con conexión/admisión y UI restablecidas. Host: 17 comprobaciones; Client: 7. Salida posterior limpia de ambos.
- Integridad: copia sin modificaciones, ninguna eliminación del destino, ningún cambio inesperado en archivos versionados, paquetes y ProjectSettings generales conservados. Sin ramas nuevas, commit, push, pull, merge, rebase ni reset.

## Pendiente y límites

No se identificó una funcionalidad que haya quedado sin trasladar. No se ejecutó una campaña completa ni se verificó matchmaking/Relay por Internet; la prueba de red utilizó el adaptador local de pruebas existente.

Quedan 26 referencias antiguas sin resolver que también están ausentes en el origen (movimientos antiguos de controladores, referencias serializadas de escenas/materiales y dos escenas deshabilitadas). No representan archivos omitidos durante la copia; se documentaron y no se corrigieron en esta etapa. Las incidencias previas de PvPvE, animaciones, interfaces, mercader y balance quedan para la etapa posterior solicitada.

Registros, respaldo previo de cada archivo combinado y manifiestos: `C:/Users/andre/Documents/Codex/2026-10-28/el-escenario-no-esta-completo-pero-5/work/pvpve-migration-develop/`. `FinalVerification.json` contiene los resultados y la lista de archivos versionados modificados.
