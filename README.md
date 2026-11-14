# Dead Quarantine

**Dead Quarantine** es un videojuego de acción y supervivencia en primera persona desarrollado con **Unity 6**. El proyecto reúne partidas individuales y multijugador, combate contra enemigos, progresión, gestión de recursos y modos de juego con objetivos distintos.

Este repositorio contiene el proyecto editable de Unity, incluidos sus escenas, scripts, prefabs y configuración de paquetes. El alcance y el estado de las funcionalidades evolucionan junto con el desarrollo.

## Modos y contenido

- **Rondas:** combate contra oleadas de enemigos con dificultad progresiva.
- **Historia:** avance por una campaña con escenas y puntos de control.
- **Supervivencia:** gestión de recursos y progreso persistente.
- **PvPvE:** enfrentamientos que combinan jugadores y enemigos controlados por el juego.
- **Minijuegos:** actividades opcionales integradas en el mundo, como la máquina tragamonedas y el desafío del dinosaurio.
- **Multijugador:** salas y partidas en red con selección de personaje y sincronización de juego.

El proyecto también incluye menús, tutorial, HUD e inventario, armas y accesorios, consumibles, logros, recursos intercambiables y zonas seguras. La disponibilidad puede variar según el modo y la escena.

## Tecnología

- **Unity 6 (6000.5.8f1)**, según `ProjectSettings/ProjectVersion.txt`.
- **Netcode for GameObjects** y Unity Services Multiplayer para funciones de red.
- **Input System**, **AI Navigation**, **URP** y **TextMeshPro**.
- Proyecto organizado bajo `Assets/`, con escenas principales en `Assets/Scenes/` y configuración de paquetes en `Packages/manifest.json`.

## Escenas incluidas en la compilación

Las escenas habilitadas actualmente en `ProjectSettings/EditorBuildSettings.asset` incluyen:

- `PantallasUI`: interfaz y navegación inicial.
- `NetworkLobby`: sala multijugador.
- `MainSceneSinglePlayer` y `MainSceneMultiPlayer`: partidas principales.
- `TutorialScene`: tutorial.
- `MP_Laboratorio`, `MP_Cabana`, `MP_Ciudad`, `MP_ZonaSegura1` y `MP_ZonaSegura2`: escenarios multijugador y zonas seguras.
- `PvPvEMatch`: partida PvPvE.
- `Demo` del paquete DinoRun: escena de demostración del minijuego.

La lista puede cambiar; la configuración del editor es la referencia para saber qué escenas se incluyen en una compilación.

## Requisitos

- Unity Hub.
- La versión de Unity indicada en `ProjectSettings/ProjectVersion.txt`.
- Módulos de compilación para la plataforma de destino, si se quiere generar un ejecutable.

Los paquetes requeridos se descargan al abrir el proyecto desde Unity Hub o Unity Editor.

## Abrir el proyecto

1. Clona el repositorio:

   ```bash
   git clone https://github.com/GregVillalba/DeadQuarantine.git
   ```

2. En Unity Hub, selecciona **Add** y elige la carpeta clonada.
3. Abre el proyecto con la versión indicada en `ProjectSettings/ProjectVersion.txt` y espera a que Unity importe los recursos.
4. Para ejecutar una partida desde el editor, abre la escena inicial desde `Assets/Scenes/` o inicia desde la escena de interfaz.

## Controles

Los controles dependen del modo y de la configuración activa. El esquema de entrada del jugador se encuentra en `Assets/Scripts/Player/PlayerControls.inputactions`; consulta también las indicaciones mostradas en el juego.

## Estado del proyecto

El proyecto sigue en desarrollo. El backlog de producto es el registro de referencia para consultar historias, prioridades y estado de avance; algunas funcionalidades pueden estar en implementación o requerir ajustes por modo de juego.

## Equipo

Proyecto desarrollado por el **Grupo 6, Comisión 1**, para **Laboratorio de Construcción de Software (PP1)** de la **Universidad Nacional de General Sarmiento (UNGS)**.
