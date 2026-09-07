# Dead Quarantine — FPS Cooperativo de Supervivencia 🧟‍♂️🔫

**Dead Quarantine** es un prototipo de videojuego de acción y disparos en primera persona (FPS) cooperativo para hasta 2 jugadores desarrollado en **Unity 6**. El proyecto fue creado en el marco de la materia **Laboratorio de Construcción de Software (PP1)** de la **Universidad Nacional de General Sarmiento (UNGS)**.

Inspirado en los clásicos modos de supervivencia por hordas, los jugadores deben resistir 5 rondas consecutivas dentro de una casa abandonada, enfrentando hordas progresivas de zombis y derrotando a un temible Zombie Boss final.

---

## 👥 Equipo de Desarrollo (Grupo 6 - Comisión 1)

* **Gregorio Villalba** — *Product Owner & Desarrollador*
* **Luciana Oviedo** — *Scrum Master & Desarrollador & QA Tester*
* **Martina Soria** — *Desarrollador & QA Tester*
* **Julian Kidjekouchian** — *Desarrollador*

* **Docentes:** Juan Carlos Monteros / Amin Sajud  
* **Fecha de entrega:** 07/09/2026

---

## 🎮 Mecánicas y Funcionalidades del Juego

### 1. Sistema de Juego y Progresión
* **5 Rondas Progresivas:** En cada ronda la cantidad de zombis y su velocidad aumentan (comienzan caminando y luego pasan a correr). Cada zombi eliminado otorga 500 puntos.
* **Zombie Boss (Ronda 5):** En la ronda final aparece un zombi gigante con mayor escala (1.5x), multiplicador de vida y daño incrementado.
* **Condición de Victoria:** Sobrevivir a las 5 rondas y derrotar al Boss junto a los zombis remanentes.
* **Condición de Derrota:** 
  * *Singleplayer:* Al llegar a 0 de vida se pierde la partida.
  * *Multiplayer:* Si un jugador cae, pasa a **modo espectador** durante esa ronda; si su compañero sobrevive y limpia la ronda, reaparece con salud completa en la siguiente. La derrota ocurre si ambos caen en la misma ronda o agotan sus vidas. **UNICAMENTE 2 JUGADORES**
* **Regeneración de Salud:** Alejarse del peligro y evitar contacto con los zombis permite regenerar la salud progresivamente hasta el tope.

### 2. Entorno 3D Interactivo (Casa Abandonada)
* **Exploración Vertical:** Casa de dos plantas modelada con ProBuilder, con escaleras transitables y patio exterior cerrado con rejas.
* **Puertas Funcionales:** Se pueden abrir y cerrar dinámicamente para gestionar rutas de escape o contención.
* **Ventanas Atravesables:** Ventanas transitables que el jugador puede saltar/atravesar para huir hacia el exterior.
* **Físicas y Colisiones Sólidas:** Geometría delimitada para prevenir caídas fuera de mapa y atravesamiento de muros o muebles.

### 3. Arsenal y Combate
* **Pistola Inicial:** Arma base con recarga táctica (capacidad de cargador de 12 balas).
* **Estación de Compra en Pared:** Ubicada cerca de la escalera, permite comprar un rifle automático interactuando a cambio de puntos.
* **Alternancia de Armamento:** Tecla rápida para alternar entre pistola y rifle sin perder las armas adquiridas.
* **Apuntado FPS:** Modo apuntado (ADS) con clic derecho y disparo directo con clic izquierdo.

### 4. Arquitectura Multijugador (Host / Client)
* **Arquitectura de Red:** Desarrollado con **Netcode for GameObjects (NGO)** y Unity Transport.
* **Servidor No Dedicado (Host):** El host procesa la lógica autoritativa del juego (IA con NavMesh, spawn de enemigos, cálculo de daño y avance de rondas).
* **Sincronización:** Posiciones y rotaciones replicadas mediante `NetworkTransform` con interpolación para evitar jittering. Las cámaras locales, inputs y HUD están desacoplados (`IsOwner`) para evitar conflictos entre pantallas.
* **Conexión:** Unión mediante código de sala o dirección IP local/Relay.

### 5. Encuesta de Valoración Automatizada (CSAT)
* Al finalizar la sesión (Victoria o Derrota), se despliega una pantalla de feedback directo (1 a 5 estrellas).
* Los datos se envían de forma desatendida mediante un webhook a **Google Apps Script** y se consolidan en una planilla de **Google Sheets** para calcular el índice CSAT en tiempo real.

---

## ⌨️ Controles

| Acción | Tecla / Control |
| :--- | :--- |
| **Moverse** | `W` `A` `S` `D` |
| **Mirar / Rotar cámara** | Movimiento del `Mouse` |
| **Saltar** | `Espacio` |
| **Atravesar ventana** | `Espacio` (pegado a ventana transitable) |
| **Correr (Sprint)** | Mantener `Shift` (consume stamina) |
| **Agacharse** | Mantener / presionar `Ctrl` |
| **Apuntar (ADS)** | Mantener `Clic Derecho` |
| **Disparar** | `Clic Izquierdo` |
| **Recargar** | `R` |
| **Interactuar (Puertas / Compra de arma)** | `E` |
| **Cambiar de arma** | `1` / `2` |
| **Pausar / Menú de Configuración** | `Esc` |

*(Nota: En el menú de opciones/pausa se puede ajustar la sensibilidad del ratón y el volumen general del juego, configuraciones que quedan guardadas en el sistema).*

---

## 🕹️ Cómo Jugar

### Modo Un Jugador (Singleplayer)
1. En el menú principal, haz clic en **Iniciar Partida**.
2. Selecciona **Un Jugador (Singleplayer)**.
3. Pulsa **Continuar** tras la pantalla de historia para ingresar a la casa y sobrevivir a las 5 hordas.

### Modo Multijugador Cooperativo (2 Jugadores)
* **Si eres el Anfitrión (Host):**
  1. En el menú selecciona **Multijugador** → **Crear Sala (Host)**.
  2. Espera en la sala de espera y comparte el código / IP con tu compañero.
  3. Cuando ambos estén listos, iniciará la cuenta regresiva para entrar a la partida.
* **Si eres el Invitado (Client):**
  1. Selecciona **Multijugador** → **Unirse a Sala (Client)**.
  2. Ingresa el código provisto por el host y presiona **Listo**.

---

## 📦 Descarga y Ejecución

### Opción A: Probar el Ejecutable (.exe) directo
1. Descarga el paquete compilado desde la sección de **Releases** o el enlace del Anexo del informe.
2. Descomprime el archivo `.zip` en una carpeta local.
3. Ejecuta directamente `DeadQuarantine.exe`. No requiere configuraciones externas ni software adicional.

### Opción B: Abrir desde el Editor de Unity
1. **Requisitos:**
   * **Unity Hub** instalado.
   * Versión de motor: **Unity 6 (6000.x)** con soporte de compilación para Windows (Mono/IL2CPP).
   * Paquetes clave: *Netcode for GameObjects*, *Input System (v2)*, *AI Navigation*, *ProBuilder* y *TextMeshPro*.
2. **Clonar repositorio:**
   ```bash
   git clone [https://github.com/GregVillalba/DeadQuarantine.git](https://github.com/GregVillalba/DeadQuarantine.git)
