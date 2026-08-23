# Dead Quarantine — FPS Cooperativo

**Dead Quarantine** es un FPS cooperativo desarrollado en **Unity 6** como trabajo universitario. Un juego de acción donde los jugadores deben sobrevivir en un ambiente hostil, utilizando armas, estrategia y cooperación para completar objetivos.

---

## 📋 Descripción General

- **Motor:** Unity 6 (URP — Universal Render Pipeline)
- **Lenguaje:** C#
- **Género:** FPS Cooperativo
- **Estado:** En desarrollo (Etapa 2 — Sistema de movimiento, disparo y UI completados)

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

---

## 🏗️ Estructura de Carpetas
