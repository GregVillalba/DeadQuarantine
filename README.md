# Dead Quarantine — FPS Cooperativo

**Dead Quarantine** es un FPS cooperativo desarrollado en **Unity 6** como trabajo universitario. Un juego de acción donde los jugadores deben sobrevivir en un ambiente hostil, utilizando armas, estrategia y cooperación para completar objetivos.

---

## 📋 Descripción General

- **Motor:** Unity 6 (URP — Universal Render Pipeline)
- **Lenguaje:** C#
- **Género:** FPS Cooperativo
- **Estado:** En desarrollo (Etapa 2 — Sistema de movimiento, disparo y UI completados)

---
## 🚀 Guía de Inicio Rápido 

Esta guía te ayudará a descargar, configurar y ejecutar el juego **sin necesidad de experiencia previa** con Unity.

### 📥 Paso 1: Descargar el Proyecto

1. Ve a: [https://github.com/GregVillalba/DeadQuarantine](https://github.com/GregVillalba/DeadQuarantine)
2. Haz clic en el botón **Code** (color verde)
3. Selecciona **Download ZIP**
4. Descomprime la carpeta en tu computadora

### 🎮 Paso 2: Instalar Unity

1. Descarga **Unity Hub** desde: https://unity.com/download
2. Instala Unity 6 
3. Abre Unity Hub

### 🔧 Paso 3: Abrir el Proyecto

1. En Unity Hub, haz clic en **"Add"**
2. Selecciona **"Add project from disk"**
3. Navega a la carpeta `DeadQuarantine` que descargaste
4. Haz clic en seleccionar
5. Espera a que Unity **compile** el proyecto (puede tardar 2-5 minutos)

### 📂 Paso 4: Entender las Escenas

El juego tiene dos escenas principales:

- **`PantallasUI`** ← **Escena del menú principal** (es donde quieres empezar)
- **`SampleScene`** ← Donde ocurre el gameplay

### 🎬 Paso 5: Cargar la Escena del Menú

1. En el panel izquierdo (**Project**), navega a: Assets > Scenes > PantallasUI.unity
2. **Haz doble clic** en `PantallasUI` para cargarla (aqui comienza el proyecto)

### ▶️ Paso 6: Ejecutar el Juego

1. Busca el botón **▶️ Play** (verde) en la parte superior del editor
2. Haz clic en él
3. El juego se ejecutará dentro del editor de Unity
4. Verás la **pantalla del menú principal** funcional

### 🎨 Lo que Verás

Cuando ejecutes la escena `PantallasUI`, obtendrás:

- ✅ Pantalla de menú principal con botones interactivos
- ✅ Sistema de navegación entre pantallas
- ✅ Controles de pausa y salida
- ✅ Interfaz completa del juego

### 📁 Archivos Clave del Menú
Assets/
├── Scenes/
│   ├── PantallasUI.unity        ← MENÚ PRINCIPAL
│   └── MainScene.unity        ← Gameplay
├── Scripts/
│   └── GameplayPopupsController.cs  ← Controla el menú e historia dentro del juego
│   └── ButtonHoverMenuPrincipal.cs  ← funcion :hover en botones
│   └── MenuPrincipalAcciones.cs     ← funciones de los botones en los menús prinicpales
└── Figma/
    └── Screens/
        └── main-menu.prefab     ← Diseño del menú
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
- ✅ Dispersión dinámica (variable según estado del jugador: en reposo, en movimiento, agachado, apuntando)
- ✅ Retroceso (recoil) visual al disparar
- ✅ Munición limitada (6 disparos)
- ✅ Sistema de recarga (atajo R)

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
- ✅ Barra de salud
- ✅ Regeneración automática (después de 4 segundos sin daño)
- ✅ Color dinámico de la barra (verde → naranja → rojo)

### HUD (Interfaz de Usuario)
- ✅ Contador de munición
- ✅ Barra de estamina
- ✅ Barra de salud con texto
- ✅ Crosshair dinámico (se expande con la dispersión)

### Mecánica de Arma
- ✅ Bob de arma (movimiento oscilante al caminar)
- ✅ Animación suave de recarga (visual, no física)


