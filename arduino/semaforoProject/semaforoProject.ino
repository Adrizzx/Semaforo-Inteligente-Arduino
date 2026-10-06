/**
 * @file semaforoProject.ino
 * @author [Lenin Barrionuevo | Adrián Padilla | Raphael Galarza]
 * @brief Sistema de control integral para un semáforo inteligente.
 * @version 2.0 (Refactorizado)
 * @date 2024-07-31
 *
 * @details
 * Este código controla un sistema de semáforo multifuncional que integra:
 * - Control de LEDs para semáforo vehicular y peatonal.
 * - Gestión de un display de 7 segmentos mediante una EEPROM externa (AT28C64B).
 * - Lectura de un teclado matricial para comandos locales.
 * - Reproducción de melodías de estado a través de un buzzer.
 * - Despliegue de animaciones en una matriz de LEDs 8x8 (MAX7219).
 * - Interfaz de comunicación serial para control remoto desde una aplicación anfitriona (Host).
 * El sistema está diseñado sobre una arquitectura no bloqueante para máxima capacidad de respuesta.
 *
 * ======================================================================================
 * ⚡ MAPA DE ASIGNACIÓN DE PINES (ARDUINO UNO/NANO) ⚡
 * ======================================================================================
 * PIN ARDUINO | IDENTIFICADOR         | MÓDULO / DESCRIPCIÓN
 * ----------- | --------------------- | ----------------------------------------------------------------------
 * Digital 2   | MATRIZ_DIN            | (Data IN) para la matriz de LEDs MAX7219.
 * Digital 3   | LED_ROJO              | LED rojo del semáforo vehicular.
 * Digital 4   | LED_AMARILLO          | LED amarillo del semáforo vehicular.
 * Digital 5   | LED_VERDE             | LED verde del semáforo vehicular.
 * Digital 6   | LED_PEATON            | LED verde para el cruce peatonal.
 * Digital 7   | MATRIZ_CS             | (Chip Select) para la matriz de LEDs MAX7219.
 * Digital 8   | EEPROM_A0             | Línea de dirección 0 (LSB) para la EEPROM AT28C64B.
 * Digital 9   | EEPROM_A1             | Línea de dirección 1 para la EEPROM AT28C64B.
 * Digital 10  | EEPROM_A2             | Línea de dirección 2 (MSB) para la EEPROM AT28C64B.
 * Digital 11  | KEYPAD_COL_1          | Columna 1 del teclado matricial.
 * Digital 12  | KEYPAD_COL_2          | Columna 2 del teclado matricial.
 * Digital 13  | MATRIZ_CLK            | (Clock) para la matriz de LEDs MAX7219.
 * Analog A0   | KEYPAD_ROW_1          | Fila 1 del teclado matricial (configurado como 1x4).
 * Analog A1   | PIN_BUZZER            | Buzzer para la salida de audio.
 * Analog A2   | KEYPAD_COL_3          | Columna 3 del teclado matricial.
 * Analog A3   | KEYPAD_COL_4          | Columna 4 del teclado matricial.
 * Analog A4   | I2C_SDA               | (Serial Data) para comunicación I2C (usado por el LCD).
 * Analog A5   | I2C_SCL               | (Serial Clock) para comunicación I2C (usado por el LCD).
 *
 * --------------------------------------------------------------------------------------
 * Para más detalles sobre la conexión de la EEPROM al display de 7 segmentos,
 * referirse al diagrama ASCII al final de este archivo.
 * --------------------------------------------------------------------------------------
 */

//======================================================================================
// SECCIÓN DE LIBRERÍAS Y CONFIGURACIONES GLOBALES
//======================================================================================

// --- LIBRERÍAS ---
#include <Keypad.h>          // Para la gestión del teclado matricial.
#include <LedControl.h>      // Para el control del driver MAX7219 de la matriz de LEDs.
#include <LiquidCrystal_I2C.h>// Para el control del LCD 16x2 a través de I2C.

// --- CONFIGURACIÓN DE DISPOSITIVOS I2C Y MATRICES ---
LiquidCrystal_I2C lcd(0x27, 16, 2); // Objeto LCD: Dirección I2C 0x27, 16 columnas, 2 filas.
LedControl matriz = LedControl(2, 13, 7, 1); // Objeto Matriz: (DIN, CLK, CS, Num_Matrices).

// --- CONFIGURACIÓN DEL TECLADO MATRICIAL (Modo 1x4) ---
const byte FILAS = 1;
const byte COLUMNAS = 4;
const char teclas[FILAS][COLUMNAS] = {{'1', '2', '3', 'A'}};
const byte pinesFilas[FILAS] = {A0};
const byte pinesColumnas[COLUMNAS] = {11, 12, A2, A3};
Keypad teclado = Keypad(makeKeymap(teclas), pinesFilas, pinesColumnas, FILAS, COLUMNAS);

// --- DEFINICIONES DE PINES ---
// LEDs del Semáforo
const int LED_ROJO = 3;
const int LED_AMARILLO = 4;
const int LED_VERDE = 5;
const int LED_PEATON = 6;
// Buzzer
const int PIN_BUZZER = A1;
// Líneas de dirección para la EEPROM
const int EEPROM_A0 = 8;
const int EEPROM_A1 = 9;
const int EEPROM_A2 = 10;

// --- VARIABLES DE ESTADO GLOBALES ---
String estadoActual = "APAGADO";      // Máquina de estados principal. Inicia en reposo.
String colorCarroActual = "";         // Almacena el color de coche recibido para el display.
String comandoSerial = "";            // Buffer para ensamblar comandos desde el puerto serial.
bool programaActivo = false;          // Flag para habilitar/deshabilitar las animaciones y melodías.

// --- DEFINICIONES PARA ANIMACIONES Y MELODÍAS ---
// Intervalo de tiempo para la actualización de frames de animación (en milisegundos).
const int intervaloAnimacion = 200;
// Variables para la gestión de temporizadores no bloqueantes.
unsigned long tiempoUltimoFrame = 0;
unsigned long ultimoTono = 0;
// Contadores de secuencia para animaciones y melodías.
int frameActual = 0;
int indiceNota = 0;

// --- MAPA DE DATOS PARA DISPLAY 7-SEGMENTOS ---
// Este array define los colores que el sistema puede mostrar en el display.
// La posición (índice) en este array se usará como la dirección para la EEPROM.
String colores[] = {
  "Rojo", "Azul", "Verde", "Amarillo", "Negro", "Blanco", "Naranja", "Lila"
};
// Calcula automáticamente el número de colores. Más mantenible que un número fijo.
int totalColores = sizeof(colores) / sizeof(colores[0]);

// --- DATOS EN MEMORIA PROGMEM ---
// PROGMEM se utiliza para almacenar grandes bloques de datos constantes en la memoria Flash
// en lugar de la SRAM, que es muy limitada (2KB en un Arduino Uno). Esto es CRÍTICO
// para evitar que el programa se quede sin memoria y se comporte de forma errática.

// Melodías (Notas y Duraciones)
// Las notas están definidas por sus frecuencias en Hz.
// Cada melodía tiene un array de notas y un array de duraciones correspondiente.
const int PROGMEM melodiaVerde[] = {659, 659, 659, 523, 659, 783, 523, 391, 329, 440, 493, 466, 440, 391, 659, 783, 880, 698, 783, 659, 523, 587, 493, 523, 391, 329, 440, 493, 466, 440, 391, 659, 783, 880, 698, 783, 659, 523, 587, 493, 783, 739, 698, 622, 659, 415, 440, 523, 440, 523, 587, 783, 739, 698, 622, 659, 1046, 1046, 1046, 783, 739, 698, 622, 659, 415, 440, 523, 440, 523, 587, 622, 587, 523, 783, 739, 698, 622, 659, 415, 440, 523, 440, 523, 587, 783, 739, 698, 622, 659, 1046, 1046, 1046, 783, 739, 698, 622, 659, 415, 440, 523, 440, 523, 587, 622, 587, 523, 523, 523, 523, 587, 659, 523, 440, 391, 523, 523, 523, 523, 587, 659, 523, 523, 523, 523, 587, 659, 523, 440, 391, 659, 659, 659, 523, 659, 783, 523, 391, 329, 440, 493, 466, 440, 391, 659, 783, 880, 698, 783, 659, 523, 587, 493, 523, 391, 329, 440, 493, 466, 440, 391, 659, 783, 880, 698, 783, 659, 523, 587, 493, 659, 523, 391, 415, 440, 698, 698, 440, 493, 880, 880, 880, 783, 698, 659, 523, 440, 391, 659, 523, 391, 415, 440, 698, 698, 440, 493, 698, 698, 698, 659, 587, 523};
const int PROGMEM duracionesVerde[] = {125, 250, 250, 125, 250, 1000, 375, 375, 375, 250, 250, 125, 250, 166, 166, 166, 250, 125, 250, 250, 125, 125, 375, 375, 375, 375, 250, 250, 125, 250, 166, 166, 166, 250, 125, 250, 250, 125, 125, 625, 125, 125, 125, 250, 250, 125, 125, 250, 125, 125, 375, 125, 125, 125, 250, 250, 250, 125, 750, 125, 125, 125, 250, 250, 125, 125, 250, 125, 125, 375, 375, 375, 1250, 125, 125, 125, 250, 250, 125, 125, 250, 125, 125, 375, 125, 125, 125, 250, 250, 250, 125, 750, 125, 125, 125, 250, 250, 125, 125, 250, 125, 125, 375, 375, 375, 1000, 125, 250, 250, 125, 250, 125, 250, 125, 500, 125, 250, 250, 125, 125, 1125, 125, 250, 250, 125, 250, 125, 250, 125, 500, 125, 250, 125, 166, 166, 166, 166, 166, 125, 250, 125, 500, 125, 250, 375, 250, 125, 250, 125, 500, 125, 250, 125, 166, 166, 166, 1000};
const int PROGMEM melodiaAmarillo[] = {440, 494, 523, 587, 659};
const int PROGMEM duracionesAmarillo[] = {150, 150, 150, 150, 300};
const int PROGMEM melodiaRojo[] = {523, 659, 523, 659, 523, 659, 523, 659};
const int PROGMEM duracionesRojo[] = {200, 200, 200, 200, 200, 200, 200, 200};

// Animaciones (Frames de 8x8 bytes)
const byte PROGMEM animacionAmarillo[8] = {B00000000, B00100100, B01000010, B10011001, B10111101, B01000010, B00100100, B00000000};
const byte PROGMEM peaton1[8] = {B00011000, B00011000, B00011000, B00111100, B00011000, B00100100, B01000010, B00000000};
const byte PROGMEM peaton2[8] = {B00011000, B00011000, B00011000, B00111100, B00011000, B00011000, B00100100, B01000010};
const byte PROGMEM peaton3[8] = {B00011000, B00011000, B00011000, B00111100, B00011000, B00100100, B01011010, B00000000};
const byte PROGMEM autoFrames[8][8] = {{B00000000, B00000010, B01111111, B11111111, B01111111, B00000010, B00000000, B00000000}, {B00000000, B00000001, B10111111, B11111111, B10111111, B00000001, B00000000, B00000000}, {B00000000, B10000000, B11011111, B11111111, B11011111, B10000000, B00000000, B00000000}, {B00000000, B01000000, B11101111, B11111111, B11101111, B01000000, B00000000, B00000000}, {B00000000, B00100000, B11110111, B11111111, B11110111, B00100000, B00000000, B00000000}, {B00000000, B00010000, B11111011, B11111111, B11111011, B00010000, B00000000, B00000000}, {B00000000, B00001000, B11111101, B11111111, B11111101, B00001000, B00000000, B00000000}, {B00000000, B00000100, B11111110, B11111111, B11111110, B00000100, B00000000, B00000000}};


//======================================================================================
// FUNCIÓN DE CONFIGURACIÓN INICIAL (setup)
//======================================================================================
/**
 * @brief Se ejecuta una sola vez al energizar o reiniciar el Arduino.
 * @details Inicializa todos los componentes de hardware (Serial, LCD, Matriz),
 * configura los pines como entradas o salidas y establece el estado
 * inicial del sistema en un modo de reposo seguro (todo apagado).
 */
void setup() {
  // Inicializa la comunicación serial para depuración y control.
  Serial.begin(9600);

  // Inicializa el LCD 16x2.
  lcd.init();
  lcd.backlight();
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Semaforo Online");
  lcd.setCursor(0, 1);
  lcd.print("Esperando Comand");

  // Configura el pin del buzzer como salida de audio.
  pinMode(PIN_BUZZER, OUTPUT);

  // Inicializa la matriz de LEDs MAX7219.
  matriz.shutdown(0, false);   // Activa el chip (sale del modo de bajo consumo).
  matriz.setIntensity(0, 8); // Fija un brillo intermedio (rango 0-15).
  matriz.clearDisplay(0);    // Asegura que la matriz esté limpia al arrancar.

  // Configura los pines de los LEDs como salidas.
  pinMode(LED_ROJO, OUTPUT);
  pinMode(LED_AMARILLO, OUTPUT);
  pinMode(LED_VERDE, OUTPUT);
  pinMode(LED_PEATON, OUTPUT);

  // Configura los pines de control de dirección de la EEPROM como salidas.
  pinMode(EEPROM_A0, OUTPUT);
  pinMode(EEPROM_A1, OUTPUT);
  pinMode(EEPROM_A2, OUTPUT);

  // Garantiza un estado inicial seguro con todos los LEDs apagados.
  digitalWrite(LED_ROJO, LOW);
  digitalWrite(LED_AMARILLO, LOW);
  digitalWrite(LED_VERDE, LOW);
  digitalWrite(LED_PEATON, LOW);

  // Asegura que el display 7-segmentos esté apagado.
  mostrarEnDisplay(' ');

  Serial.println("=== SEMAFORO LISTO - ESPERANDO COMANDOS DE C# ===");
}

//======================================================================================
// BUCLE PRINCIPAL DE EJECUCIÓN (loop)
//======================================================================================
/**
 * @brief Es el corazón del programa, se ejecuta continuamente.
 * @details Este bucle está diseñado para ser "no bloqueante". En lugar de usar
 * `delay()`, sondea constantemente en busca de eventos (teclado, serial)
 * y actualiza las máquinas de estado (animación, melodía) basándose
 * en el tiempo transcurrido, garantizando la máxima capacidad de respuesta.
 */
void loop() {
  // --- 1. Lectura del Teclado Matricial ---
  char tecla = teclado.getKey();
  if (tecla) {
    Serial.print("Tecla presionada: ");
    Serial.println(tecla);
  }

  // --- 2. Lectura de Comandos Seriales (Método NO BLOQUEANTE y más robusto) ---
  while (Serial.available() > 0) {
    char caracterLeido = (char)Serial.read();

    // Si el carácter es un retorno de carro ('\r'), lo ignoramos.
    if (caracterLeido == '\r') {
      // No hacer nada
    }
    // Si es un salto de línea ('\n'), significa que el comando terminó y debemos procesarlo.
    else if (caracterLeido == '\n') {
      comandoSerial.trim(); // Limpiamos espacios en blanco al inicio o final.
      
      // Solo procesamos si el comando no está vacío.
      if (comandoSerial.length() > 0) {
        if (comandoSerial.startsWith("COLOR:")) {
          colorCarroActual = comandoSerial.substring(6);
          mostrarColorEnDisplay(colorCarroActual);
        } else {
          procesarComando(comandoSerial);
        }
      }
      // Indispensable: Reiniciamos la variable para el próximo comando.
      comandoSerial = "";
    }
    // Si es cualquier otro caracter, lo vamos añadiendo al string del comando.
    else {
      comandoSerial += caracterLeido;
    }
  }

  // --- 3. Actualización de Animaciones y Melodías ---
  if (programaActivo) {
    actualizarAnimacionEstado();
    reproducirMelodia();
  } else {
    noTone(PIN_BUZZER);
    matriz.clearDisplay(0);
  }
}

//======================================================================================
// SECCIÓN DE FUNCIONES AUXILIARES
//======================================================================================

/**
 * @brief Enruta los comandos de estado recibidos a la acción correspondiente.
 * @param comando El comando de texto (ej. "VERDE") a procesar.
 * @details Esta es la función central que gestiona los cambios de estado del semáforo.
 * Activa el sistema y actualiza los LEDs, LCD y estado global.
 */ 
void procesarComando(String comando) {
  // Diagnóstico: Imprime el comando entre corchetes para ver espacios ocultos.
  Serial.print("Comando para procesar: [");
  Serial.print(comando);
  Serial.println("]");

  // Maneja el comando "APAGAR".
  if (comando == "APAGAR") {
    programaActivo = false;
    apagarTodos(); // Llama a apagarTodos para que también silencie el buzzer.
    Serial.println(">> SISTEMA APAGADO");
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("SISTEMA APAGADO");
    lcd.setCursor(0, 1);
    lcd.print("Esperando orden");
    return;
  }
  
  // Si el comando no es para apagar, nos aseguramos de que el programa se active.
  programaActivo = true;

  // Maneja el comando "VERDE".
  if (comando == "VERDE") {
    apagarTodos();
    digitalWrite(LED_VERDE, HIGH);
    digitalWrite(LED_PEATON, LOW);
    estadoActual = "VERDE";
    Serial.println(">> Estado cambiado a VERDE");
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Estado: VERDE");
    lcd.setCursor(0, 1);
    lcd.print("Carros avanzan");
  }
  // Maneja el comando "AMARILLO".
  else if (comando == "AMARILLO") {
    apagarTodos();
    digitalWrite(LED_AMARILLO, HIGH);
    digitalWrite(LED_PEATON, LOW);
    estadoActual = "AMARILLO";
    Serial.println(">> Estado cambiado a AMARILLO");
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Estado: AMARILLO");
    lcd.setCursor(0, 1);
    lcd.print("PRECAUCION");
  }
  // Maneja el comando "ROJO".
  else if (comando == "ROJO") {
    apagarTodos();
    digitalWrite(LED_ROJO, HIGH);
    digitalWrite(LED_PEATON, HIGH);
    estadoActual = "ROJO";
    mostrarEnDisplay(' ');
    Serial.println(">> Estado cambiado a ROJO");
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Estado: ROJO");
    lcd.setCursor(0, 1);
    lcd.print("DETENGA EL AUTO");
  }
  // Cazador de errores: Si el comando no es ninguno de los anteriores.
  else {
    Serial.println(">> Comando NO RECONOCIDO.");
  }
}

/**
 * @brief Traduce un nombre de color a una dirección de 3 bits para la EEPROM.
 * @param color El nombre del color (String) a buscar.
 * @details Este método optimizado itera sobre el array `colores`. El índice del color
 * coincidente se usa directamente como la dirección, haciendo el sistema
 * fácilmente escalable a nuevos colores.
 */
void mostrarColorEnDisplay(String color) {
  int direccion = -1; // Inicia con un valor inválido.

  // Itera a través del array de colores para encontrar el índice coincidente.
  for (int i = 0; i < totalColores; i++) {
    if (colores[i] == color) {
      direccion = i; // El índice es la dirección.
      break;       // Termina el bucle una vez encontrado.
    }
  }

  // Si se encontró un color válido, establece los pines de dirección de la EEPROM.
  if (direccion != -1) {
    digitalWrite(EEPROM_A0, (direccion & B001) ? HIGH : LOW); // Bit 0
    digitalWrite(EEPROM_A1, (direccion & B010) ? HIGH : LOW); // Bit 1
    digitalWrite(EEPROM_A2, (direccion & B100) ? HIGH : LOW); // Bit 2
  }
}

/**
 * @brief Establece una dirección específica en la EEPROM. Usado para limpiar el display.
 * @param letra Por ahora, solo maneja ' ' para apagar el display (dirección 000).
 */
void mostrarEnDisplay(char letra) {
  if (letra == ' ') {
    digitalWrite(EEPROM_A0, LOW);
    digitalWrite(EEPROM_A1, LOW);
    digitalWrite(EEPROM_A2, LOW);
  }
}

/**
 * @brief Devuelve la inicial de un color para una depuración más legible en el monitor serial.
 * @param color El nombre del color (String).
 * @return El carácter inicial representativo.
 */
char obtenerLetraColor(String color) {
  if (color == "Rojo") return 'R';
  if (color == "Azul") return 'Z'; // 'Z' para evitar colisión con 'A' de Amarillo.
  if (color == "Verde") return 'V';
  if (color == "Amarillo") return 'A';
  if (color == "Negro") return 'N';
  if (color == "Blanco") return 'B';
  if (color == "Naranja") return 'J'; // 'J' de Naranja.
  if (color == "Lila") return 'L';
  return '?'; // Carácter para colores no reconocidos.
}

/**
 * @brief Realiza un reseteo completo de todas las salidas visuales y auditivas.
 * @details Es una función CRÍTICA que se llama antes de cada cambio de estado para
 * asegurar una transición limpia. Detener el buzzer con `noTone()` es
 * esencial para liberar los temporizadores de hardware y evitar conflictos.
 */
void apagarTodos() {
  noTone(PIN_BUZZER);
  digitalWrite(LED_ROJO, LOW);
  digitalWrite(LED_AMARILLO, LOW);
  digitalWrite(LED_VERDE, LOW);
  digitalWrite(LED_PEATON, LOW);
  matriz.clearDisplay(0); // Limpia la matriz instantáneamente
}

/**
 * @brief Dibuja un frame de 8x8 en la matriz de LEDs.
 * @param animacion Puntero a un array de 8 bytes almacenado en PROGMEM.
 * @details Utiliza `pgm_read_byte_near` para leer eficientemente los datos
 * directamente desde la memoria Flash.
 */
void mostrarAnimacion(const byte* animacion) {
  for (int fila = 0; fila < 8; fila++) {
    matriz.setRow(0, fila, pgm_read_byte_near(animacion + fila));
  }
}

/**
 * @brief Máquina de estados para las animaciones.
 * @details Se llama en cada ciclo del loop. Usa `millis()` para una temporización
 * no bloqueante. Selecciona y actualiza la animación correcta
 * basándose en la variable global `estadoActual`.
 */
void actualizarAnimacionEstado() {
  // Temporizador no bloqueante: solo ejecuta si ha pasado `intervaloAnimacion`.
  if (millis() - tiempoUltimoFrame < intervaloAnimacion) return;
  tiempoUltimoFrame = millis();

  // Lógica de animación para el estado VERDE (coche en movimiento).
  if (estadoActual == "VERDE") {
    for (int fila = 0; fila < 8; fila++) {
      matriz.setRow(0, fila, pgm_read_byte_near(&(autoFrames[frameActual][fila])));
    }
    frameActual = (frameActual + 1) % 8; // Avanza el frame y lo reinicia (0-7).
  }
  // Lógica de animación para el estado AMARILLO (símbolo parpadeante).
  else if (estadoActual == "AMARILLO") {
    static bool parpadeo = false; // Variable estática para retener el estado de parpadeo.
    if (parpadeo) {
      mostrarAnimacion(animacionAmarillo);
    } else {
      matriz.clearDisplay(0);
    }
    parpadeo = !parpadeo; // Invierte el estado para el próximo ciclo.
  }
  // Lógica de animación para el estado ROJO (peatón caminando).
  else if (estadoActual == "ROJO") {
    static int subFrame = 0; // Variable estática para la secuencia de 3 frames del peatón.
    if (subFrame == 0) mostrarAnimacion(peaton1);
    else if (subFrame == 1) mostrarAnimacion(peaton2);
    else mostrarAnimacion(peaton3);
    subFrame = (subFrame + 1) % 3; // Avanza el sub-frame y lo reinicia (0-2).
  }
  // Para cualquier otro estado (ej. "APAGADO"), asegura que la matriz esté limpia.
  else {
    matriz.clearDisplay(0);
  }
}

/**
 * @brief Máquina de estados para las melodías.
 * @details Se llama en cada ciclo del loop. Detecta un cambio en `estadoActual`
 * para cargar la melodía correspondiente. Luego, usa `millis()` para
 * reproducir cada nota de forma secuencial y no bloqueante. La melodía
 * se repite mientras el estado permanezca activo.
 */
void reproducirMelodia() {
  // Variables estáticas para retener el estado y los punteros de la melodía entre llamadas.
  static String estadoAnterior = "";
  static const int* melodiaPtr = nullptr;
  static const int* duracionesPtr = nullptr;
  static int longitud = 0;

  // Detecta un cambio de estado para cargar una nueva melodía.
  if (estadoActual != estadoAnterior) {
    indiceNota = 0;             // Reinicia al inicio de la nueva melodía.
    ultimoTono = millis();      // Reinicia el temporizador de notas.
    estadoAnterior = estadoActual; // Actualiza el estado para la próxima comparación.

    // Asigna los punteros a los arrays correctos en PROGMEM.
    if (estadoActual == "VERDE") {
      melodiaPtr = melodiaVerde;
      duracionesPtr = duracionesVerde;
      longitud = sizeof(melodiaVerde) / sizeof(int);
    } else if (estadoActual == "AMARILLO") {
      melodiaPtr = melodiaAmarillo;
      duracionesPtr = duracionesAmarillo;
      longitud = sizeof(melodiaAmarillo) / sizeof(int);
    } else if (estadoActual == "ROJO") {
      melodiaPtr = melodiaRojo;
      duracionesPtr = duracionesRojo;
      longitud = sizeof(melodiaRojo) / sizeof(int);
    } else {
      // Si el estado no tiene melodía (ej. "APAGADO"), asegura que todo esté en silencio.
      noTone(PIN_BUZZER);
      return; // No hay nada más que hacer.
    }
  }

  // Temporizador no bloqueante para reproducir la siguiente nota.
  // Solo se ejecuta si los punteros han sido inicializados.
  if (duracionesPtr != nullptr && (millis() - ultimoTono >= pgm_read_word_near(&(duracionesPtr[indiceNota])))) {
    // Reproduce la nota actual.
    tone(PIN_BUZZER, pgm_read_word_near(&(melodiaPtr[indiceNota])));
    // Reinicia el temporizador para la duración de esta nota.
    ultimoTono = millis();
    // Avanza al índice de la siguiente nota, reiniciando al final para crear un bucle.
    indiceNota = (indiceNota + 1) % longitud;
  }
}

/*
 * ======================================================================================
 * 🗺️ DIAGRAMA DE CONEXIÓN DE LA EEPROM AT28C64B AL DISPLAY 7 SEGMENTOS 🗺️
 * ======================================================================================
 * Esta sección detalla la conexión directa entre la EEPROM y el display,
 * un método que ahorra pines de Arduino al usar la EEPROM como un decodificador
 * de BCD a 7 segmentos personalizado.
 *
 * ARDUINO        EEPROM AT28C64B         DISPLAY 7-SEGMENTOS
 * -------        ---------------         -------------------
 * Pin 8   -------> A0 (Pin 10)
 * Pin 9   -------> A1 (Pin 9)
 * Pin 10  -------> A2 (Pin 8)
 *
 * GND     -------> A3-A12 (Pines 7,6,5,4,3,25,24,21,23,2,26)
 * (Las líneas de dirección no utilizadas se fijan a un estado conocido, GND)
 * GND     -------> CE (Chip Enable - Pin 20) -> LOW = Siempre habilitado.
 * GND     -------> OE (Output Enable - Pin 22) -> LOW = Salidas siempre activas.
 * 5V      -------> VCC (Pin 28)
 * GND     -------> GND (Pin 14)
 *
 * O0 (Pin 11) ---------> [Resistencia 220Ω] -> Segmento 'a'
 * O1 (Pin 12) ---------> [Resistencia 220Ω] -> Segmento 'b'
 * O2 (Pin 13) ---------> [Resistencia 220Ω] -> Segmento 'c'
 * O3 (Pin 15) ---------> [Resistencia 220Ω] -> Segmento 'd'
 * O4 (Pin 16) ---------> [Resistencia 220Ω] -> Segmento 'e'
 * O5 (Pin 17) ---------> [Resistencia 220Ω] -> Segmento 'f'
 * O6 (Pin 18) ---------> [Resistencia 220Ω] -> Segmento 'g'
 * O7 (Pin 19) ---------> [Resistencia 220Ω] -> Punto Decimal (dp)
 *
 * TIPO DE DISPLAY: El conexionado asume un display de CÁTODO COMÚN, donde el
 * pin común se conecta a GND.
 *
 * NOTA SOBRE RESISTENCIAS: Es fundamental usar una resistencia limitadora de
 * corriente para cada segmento para proteger tanto los LEDs del display
 * como las salidas de la EEPROM.
 */