<div align="center">

# Semáforo Inteligente

### Sistema embebido con Arduino controlado y monitoreado desde una aplicación de escritorio en C#, con registro en SQLite y estadísticas en tiempo real

[![Arduino](https://img.shields.io/badge/Arduino-UNO-00979D?style=for-the-badge&logo=arduino&logoColor=white)](https://www.arduino.cc/)
[![C++](https://img.shields.io/badge/C%2B%2B-Arduino-00599C?style=for-the-badge&logo=cplusplus&logoColor=white)](https://docs.arduino.cc/language-reference/)
[![C#](https://img.shields.io/badge/C%23-WinForms%20.NET%208-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/desktop/winforms/)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Proteus](https://img.shields.io/badge/Simulación-Proteus%208-1E5AA8?style=for-the-badge)](https://www.labcenter.com/)

<img src="media/hardware_1.jpg" alt="Hardware del semáforo funcionando" width="85%">

</div>

---

## Descripción

Semáforo vehicular y peatonal construido sobre **Arduino UNO** que integra hardware real (LEDs, LCD, teclado, buzzer, matriz de LEDs y una memoria **EEPROM AT28C64B** programada a mano para el display de 7 segmentos) con una **aplicación de escritorio en C#** que lo controla por **puerto serial**, guarda cada evento en **SQLite** y genera **gráficas estadísticas** en tiempo real.

El sistema se validó dos veces: en **hardware físico** sobre protoboard y en **simulación completa en Proteus 8** con comunicación serial virtual (COMPIM).

## Características

**Firmware (Arduino)**
- Ciclo de semáforo vehicular con fase peatonal y temporización configurable.
- **LCD 16x2 por I2C** con el estado actual y cuenta regresiva.
- **Matriz de LEDs 8x8 (MAX7219)** con animaciones del peatón.
- **Display de 7 segmentos** alimentado desde una **EEPROM AT28C64B** codificada para mostrar letras.
- **Teclado matricial** para control manual y **buzzer** con melodías por color.
- Protocolo de comandos por **Serial** (cambio de color, apagado, consulta de estado).

**Aplicación de escritorio (C# · WinForms)**
- Conexión al Arduino seleccionando el puerto COM.
- Control remoto del semáforo y visualización del estado en vivo.
- Registro persistente de cada cambio en **SQLite**.
- Ventana de **estadísticas** con gráficas generadas desde la base de datos.

## Arquitectura

```mermaid
flowchart LR
    subgraph PC[Aplicación de escritorio · C#]
        UI[Formulario principal] --> SP[SerialPort]
        UI --> DB[(SQLite)]
        DB --> ST[Formulario de estadísticas<br/>gráficas]
    end
    SP <-->|USB · 9600 baud| AR[Arduino UNO]
    AR --> L[LEDs vehiculares<br/>y peatonal]
    AR --> LCD[LCD 16x2 I2C]
    AR --> MX[Matriz LED 8x8<br/>MAX7219]
    AR --> EE[EEPROM AT28C64B<br/>→ display 7 segmentos]
    AR --> BZ[Buzzer]
    KP[Teclado matricial] --> AR
```

## Galería

<table>
  <tr>
    <td><img src="media/interfaz_csharp.jpg" alt="Interfaz en C#"></td>
    <td><img src="media/graficas_sqlite.jpg" alt="Gráficas desde SQLite"></td>
  </tr>
  <tr>
    <td align="center"><sub>Aplicación de control en C#</sub></td>
    <td align="center"><sub>Estadísticas generadas desde SQLite</sub></td>
  </tr>
  <tr>
    <td><img src="media/proteus.jpg" alt="Simulación en Proteus"></td>
    <td><img src="media/montaje.jpg" alt="Montaje completo"></td>
  </tr>
  <tr>
    <td align="center"><sub>Simulación en Proteus 8 con COMPIM</sub></td>
    <td align="center"><sub>Montaje con todos los componentes</sub></td>
  </tr>
</table>

<div align="center">
  <img src="media/diagrama_conexiones.jpg" alt="Diagrama de conexiones" width="70%">
  <br><sub>Diagrama completo de conexiones</sub>
</div>

## Hardware

| Componente | Uso |
|---|---|
| Arduino UNO | Controlador principal |
| LEDs rojo, amarillo, verde y peatonal | Señales del semáforo |
| LCD 16x2 con módulo I2C | Estado y cuenta regresiva |
| Matriz LED 8x8 + MAX7219 | Animación del peatón |
| EEPROM AT28C64B + display 7 segmentos | Visualización de letras codificadas |
| Teclado matricial | Control manual |
| Buzzer | Señales sonoras por fase |

## Estructura

```
├── arduino/semaforoProject/   Firmware (.ino) documentado con tabla de pines
├── desktop-app/               Solución Visual Studio (WinForms, .NET 8)
├── docs/                      Informe técnico completo (PDF)
└── media/                     Fotografías y capturas
```

## Cómo ejecutarlo

1. **Firmware:** abrir `arduino/semaforoProject/semaforoProject.ino` en Arduino IDE, instalar las librerías `Keypad`, `LedControl` y `LiquidCrystal_I2C`, y cargar en el Arduino UNO.
2. **Aplicación:** abrir `desktop-app/SEMAFORO_AVANZADO.sln` en Visual Studio 2022, restaurar paquetes NuGet y ejecutar.
3. Seleccionar el puerto COM del Arduino y conectar.

 El diseño, la codificación de la EEPROM y las pruebas están en el [informe técnico](docs/Informe_Semaforo_Inteligente.pdf).

## Equipo

| Integrante |
|---|
| **Marco Adrian Padilla Triviño** ([@Adrizzx](https://github.com/Adrizzx)) |
| Lenin Antonio Barrionuevo Rodríguez |
| Angelo Raphael Galarza Manosalvas |

<div align="center">
<sub>Computación Digital · Universidad de las Fuerzas Armadas ESPE · 2025</sub>
</div>
