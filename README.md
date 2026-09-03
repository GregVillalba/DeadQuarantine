# Dead Quarantine — FPS Cooperativo

**Dead Quarantine** es un FPS cooperativo desarrollado en **Unity 6** como trabajo universitario. Un juego de acción donde los jugadores deben sobrevivir oleadas de zombies en un ambiente hostil, utilizando armas estratégicamente.

---

## 📋 Descripción General

- **Motor:** Unity 6 (URP — Universal Render Pipeline)
- **Lenguaje:** C#
- **Género:** FPS Cooperativo
- **Plataforma:** PC (Windows)
- **Estado:** En desarrollo activo (Etapa 3 — Multijugador y Sistema de enemigos implementados)
- **Equipo:** GregVillalba, Luchy-code, martuSoria, Kidje3

---

## 🎯 Últimos Cambios (31/08/2026)

### ✨ Nuevas Características Implementadas

| Característica | Estado | Descripción |
|---|---|---|
| **Sistema de Multijugador** | ✅ Funcional | Sincronización de jugadores, lobby y sistema de conexión |
| **Boss de Zombies** | ✅ Implementado | Jefe final que aparece al completar oleadas, genera desafíos balanceados |
| **Efectos de Impacto** | ✅ Implementado | Sangre dinámica al golpear enemigos y "hitmarker" visual |
| **Mecánica de Oleadas** | ✅ Completa | Sistema de rondas escalables con dificultad progresiva |
| **Interfaz de Victoria/Derrota** | ✅ Completa | Pantallas de game over y victory funcionales |
| **Escalado de Dificultad** | ✅ Activo | Velocidad y vida de zombies aumentan por ronda |
| **Aparición Inteligente de Zombies** | ✅ Aleatoria | Intervalos de aparición dinámicos para mayor tensión |

### 🔧 Mejoras Recientes

- **Arreglos en Multijugador:** Sincronización de vida y lobby corregida
- **Mejora de Zombies:** Comportamiento mejorado, velocidad y vida escaladas
- **Navmesh Optimizado:** Recalculado para cubrir toda la casa, permitiendo navegación completa
- **Skybox y Texturas:** Ambiente visual mejorado con texturas de paredes, piso y cielo
- **UI de Historias:** Pantalla de narrativa ajustada y funcional
- **Modo Singleplayer:** Totalmente arreglado y jugable

---

## 🎮 Características Implementadas

### Sistema de Movimiento del Jugador
- ✅ Movimiento omnidireccional (WASD)
- ✅ Salto con detección de suelo
- ✅ Sprint (consumo de estamina)
- ✅ Agacharse (movimiento ralentizado, altura reducida)
- ✅ Gravedad y física con Character Controller

### Sistema de Combate
- ✅ Disparo de revolver con raycast
- ✅ Dispersión dinámica (variable según estado: reposo, movimiento, agachado, apuntando)
- ✅ Retroceso (recoil) visual al disparar
- ✅ Munición limitada (6 disparos por cargador)
- ✅ Sistema de recarga (atajo R)
- ✅ Efectos de sangre al impactar

### Aiming (ADS — Aim Down Sights)
- ✅ Cambio de posición del arma al apuntar
- ✅ Zoom de cámara independiente
- ✅ Dispersión eliminada al apuntar
- ✅ Transición suave entre estados

### Sistema de Cámara
- ✅ Rotación free-look (ratón)
- ✅ Camera Stacking (2 cámaras: mundo + arma superpuesta)
- ✅ Prevención de clipping del arma contra paredes
- ✅ Sensibilidad configurable

### Sistema de Vida
- ✅ Barra de salud visual
- ✅ Regeneración automática (después de 4 segundos sin daño)
- ✅ Color dinámico de la barra (verde → naranja → rojo)
- ✅ Sincronización en multijugador

### HUD (Interfaz de Usuario)
- ✅ Contador de munición
- ✅ Barra de estamina
- ✅ Barra de salud con valores numéricos
- ✅ Crosshair dinámico (se expande con la dispersión)
- ✅ Indicador de ronda/ola

### Mecánica de Enemigos
- ✅ Zombies con IA de patrulla y persecución
- ✅ Sistema de oleadas escalonadas
- ✅ Boss de Zombies con estadísticas aumentadas
- ✅ Velocidad y vida escaladas por ronda
- ✅ Generador de enemigos inteligente con intervalo aleatorio

### Mecánica de Arma
- ✅ Bob de arma (movimiento oscilante al caminar)
- ✅ Animación suave de recarga (visual)
- ✅ Hitmarker visual al golpear enemigos

### Modo Multijugador
- ✅ Sistema de lobby
- ✅ Sincronización de vida y munición entre jugadores
- ✅ Boss en modo multijugador

---

## 🚀 Guía de Inicio Rápido - CÓMO DESCARGAR Y JUGAR

### 📥 Opción 1: Descargar el Código Fuente

#### Paso 1: Descargar el Proyecto

1. Ve a: [https://github.com/GregVillalba/DeadQuarantine](https://github.com/GregVillalba/DeadQuarantine)
2. Haz clic en el botón **Code** (color verde)
3. Selecciona **"Download ZIP"**
4. Descomprime la carpeta en tu computadora (ej: `C:\Proyectos\DeadQuarantine`)

#### Paso 2: Instalar Unity

1. Descarga **Unity Hub** desde: https://unity.com/download
2. Instala Unity Hub en tu PC
3. Abre Unity Hub

#### Paso 3: Instalar Unity 6

1. En Unity Hub, ve a la pestaña **"Installs"**
2. Haz clic en **"Install Editor"**
3. Busca **"Unity 6"** (versión LTS recomendada)
4. Selecciona la versión 6 y haz clic en **"Install"**
5. Espera a que se complete la instalación (puede tardar 10-15 minutos)

#### Paso 4: Abrir el Proyecto en Unity

1. En Unity Hub, ve a la pestaña **"Projects"**
2. Haz clic en el botón **"Add"**
3. Selecciona **"Add project from disk"**
4. Navega a la carpeta **`DeadQuarantine`** que descargaste
5. Haz doble clic en la carpeta para seleccionarla
6. Haz clic en **"Add"** para agregar el proyecto
7. Espera a que Unity **compile** el proyecto (puede tardar 2-5 minutos en la primera carga)

#### Paso 5: Cargar la Escena del Menú Principal

1. En el panel izquierdo (**Project**), navega a: **`Assets > Scenes > PantallasUI.unity`**
2. **Haz doble clic** en **`PantallasUI`** para cargarla (este es el punto de entrada del juego)

#### Paso 6: ¡Juega!

1. Busca el botón **▶️ Play** (verde) en la parte superior del editor
2. Haz clic en él
3. El juego se ejecutará dentro del editor de Unity
4. Verás la **pantalla del menú principal** con las opciones:
   - **Singleplayer:** Juega solo contra oleadas de zombies
   - **Multiplayer:** Conéctate con otros jugadores
   - **Historia:** Lee la narrativa del juego

---

### 📥 Opción 2: Compilar un Build Ejecutable

*Esta opción es más rápida si ya tienes el proyecto abierto*

1. Ve a **File > Build Settings**
2. Asegúrate de que **`PantallasUI`** esté agregada a la lista de escenas (Scene 0)
3. Selecciona **PC, Mac & Linux Standalone** como plataforma
4. Haz clic en **"Build"**
5. Elige una carpeta para guardar el ejecutable
6. Espera a que se compile (2-5 minutos)
7. Ejecuta el archivo `.exe` generado

---

## 🎮 Cómo Jugar

### Controles Principales

| Control | Acción |
|---|---|
| **WASD** | Movimiento |
| **Espacio** | Saltar |
| **Shift** | Sprint (mantener) |
| **CTRL** | Agacharse |
| **Ratón** | Mirar alrededor |
| **Click Izq.** | Disparar |
| **Click Der.** | Apuntar (ADS) |
| **R** | Recargar |
| **ESC** | Pausa/Menú |

### Modos de Juego

#### 🎯 Singleplayer
- Sobrevive oleadas de zombies crecientes
- Vence al Boss final para completar la ronda
- Aumenta de dificultad con cada oleada

#### 👥 Multiplayer
- Coopera con hasta 3 jugadores más
- Sincronización de vida y munición
- Boss compartido y oleadas conjuntas
