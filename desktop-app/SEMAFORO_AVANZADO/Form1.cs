
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Media;

namespace SEMAFORO_AVANZADO
{
    public partial class Form1 : Form
    {
        // Puerto serial para Proteus
        //SerialPort puerto = new SerialPort("COM2", 9600);
        
        // Puerto serial para Arduino
        private SerialPort arduino;

        // Timer para animación
        private System.Windows.Forms.Timer timerAnimacion;

        // Base de datos SQLite - SÚPER FÁCIL
        private string dbPath = "SemaforoDB.sqlite";
        private string connectionString = "";

        // Posiciones
        private int posicionCarro = -80;
        private int posicionPeatonY = 350;
        private bool peatonCruzando = false;
        private int peatonAnimacion = 0;

        // Estados
        private string estadoSemaforo = "ROJO";
        private bool simulacionActiva = false;

        // Control de tiempo y velocidad
        private DateTime tiempoInicioEstado = DateTime.Now;
        private DateTime tiempoInicioSimulacion = DateTime.Now;
        private Dictionary<string, TimeSpan> tiemposPorEstado = new Dictionary<string, TimeSpan>();
        private int velocidadCarro = 5;

        // Contadores
        private int contadorCarros = 0;
        private int contadorPeatones = 0;
        private int contadorCiclos = 0;

        // Colores de carros y sus contadores
        private Color colorCarroActual = Color.Red;
        private Dictionary<string, int> contadorPorColor = new Dictionary<string, int>()
        {
            { "Rojo", 0 },
            { "Azul", 0 },
            { "Verde", 0 },
            { "Amarillo", 0 },
            { "Negro", 0 },
            { "Blanco", 0 },
            { "Naranja", 0 },
            { "Lila", 0 }
        };
        private List<Color> coloresDisponibles = new List<Color>()
        {
            Color.FromArgb(220, 20, 60),    // Rojo
            Color.FromArgb(30, 144, 255),   // Azul
            Color.FromArgb(34, 139, 34),    // Verde
            Color.FromArgb(255, 215, 0),    // Amarillo
            Color.FromArgb(40, 40, 40),     // Negro
            Color.FromArgb(245, 245, 245),  // Blanco
            Color.FromArgb(255, 140, 0),    // Naranja
            Color.FromArgb(186, 85, 211)    // Lila
        };
        private List<string> nombresColores = new List<string>()
        {
            "Rojo", "Azul", "Verde", "Amarillo", "Negro", "Blanco", "Naranja", "Lila"
        };
        private Random random = new Random();

        // Lista para guardar eventos
        private List<string> registroEventos = new List<string>();

        public Form1()
        {
            InitializeComponent();
            ConfigurarFormulario();
            ConfigurarArduino();
            CrearInterfaz();
        }

        private void ConfigurarFormulario()
        {
            this.Text = "Sistema de Semáforo Inteligente - Control Manual";
            this.Size = new Size(900, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(245, 245, 245);

            // Inicializar base de datos SQLite
            InicializarBaseDatos();
        }

        private void ConfigurarArduino()
        {
            arduino = new SerialPort();
            arduino.BaudRate = 9600;
            arduino.DataReceived += Arduino_DataReceived;

            timerAnimacion = new System.Windows.Forms.Timer();
            timerAnimacion.Interval = 50;
            timerAnimacion.Tick += TimerAnimacion_Tick;
            timerAnimacion.Start();
        }

        #region Base de datos SQLite - ERRORES CORREGIDOS

        private void InicializarBaseDatos()
        {
            try
            {
                // Ruta absoluta del archivo SQLite
                string rutaCompleta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

                // Mostrar ruta absoluta para depurar
                MessageBox.Show("Usando base de datos en:\n" + rutaCompleta, "Ruta BD", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Crear connection string
                connectionString = $"Data Source={rutaCompleta};Version=3;";

                // Si no existe el archivo, créalo
                if (!File.Exists(rutaCompleta))
                {
                    SQLiteConnection.CreateFile(rutaCompleta);
                    MessageBox.Show("✓ Archivo SQLite creado correctamente.", "SQLite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RegistrarEvento("✓ Base de datos SQLite creada automáticamente");
                }

                // Crear la tabla si no existe
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string createTable = @"
                CREATE TABLE IF NOT EXISTS RegistroEventos (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FechaHora DATETIME NOT NULL,
                    TipoEvento TEXT NOT NULL,
                    ColorVehiculo TEXT,
                    NumeroVehiculo INTEGER,
                    EstadoSemaforo TEXT NOT NULL,
                    TiempoTranscurrido REAL,
                    Velocidad INTEGER,
                    Observaciones TEXT
                )";

                    using (SQLiteCommand cmd = new SQLiteCommand(createTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }

                RegistrarEvento("✓ Base de datos SQLite lista para usar");
            }
            catch (Exception ex)
            {
                // Mensaje más detallado con el error real
                MessageBox.Show($"❌ Error al inicializar la base de datos:\n{ex.Message}\n\nRuta esperada:\n{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath)}",
                                "Error SQLite", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Evita continuar si hubo error
                connectionString = "";
            }
        }

        private void GuardarEventoEnBD(string tipoEvento, string colorVehiculo, int numeroVehiculo,
                                       string estadoSemaforo, double tiempoTranscurrido, int velocidad, string observaciones)
        {
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string query = @"INSERT INTO RegistroEventos 
                                   (FechaHora, TipoEvento, ColorVehiculo, NumeroVehiculo, 
                                    EstadoSemaforo, TiempoTranscurrido, Velocidad, Observaciones) 
                                   VALUES (@fecha, @tipo, @color, @numero, @estado, @tiempo, @velocidad, @obs)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                        cmd.Parameters.AddWithValue("@tipo", tipoEvento ?? "");
                        cmd.Parameters.AddWithValue("@color", colorVehiculo ?? "");
                        cmd.Parameters.AddWithValue("@numero", numeroVehiculo);
                        cmd.Parameters.AddWithValue("@estado", estadoSemaforo ?? "");
                        cmd.Parameters.AddWithValue("@tiempo", tiempoTranscurrido);
                        cmd.Parameters.AddWithValue("@velocidad", velocidad);
                        cmd.Parameters.AddWithValue("@obs", observaciones ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                RegistrarEvento($"⚠ Error BD: {ex.Message}");
            }
        }

        private void GuardarResumenFinal()
        {
            string tipoEvento = "Resumen";
            string colorVehiculo = ""; // No aplica para resumen
            int numeroVehiculo = contadorCarros;
            string estadoFinal = estadoSemaforo;
            double tiempoSimulacion = (DateTime.Now - tiempoInicioSimulacion).TotalSeconds;
            int velocidad = velocidadCarro;

            // Construir resumen por colores
            string observaciones = $"Ciclos: {contadorCiclos}, Peatones: {contadorPeatones}, ";
            observaciones += $"Colores: ";
            foreach (var kvp in contadorPorColor)
            {
                observaciones += $"{kvp.Key}:{kvp.Value} ";
            }

            // Guardar en la base de datos
            GuardarEventoEnBD(tipoEvento, colorVehiculo, numeroVehiculo,
                              estadoFinal, tiempoSimulacion, velocidad, observaciones.Trim());
        }

        private DataTable ObtenerRegistrosBD()
        {
            DataTable dt = new DataTable();

            if (string.IsNullOrEmpty(connectionString)) return dt;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT * FROM RegistroEventos ORDER BY FechaHora DESC";

                    using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(query, conn))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer registros: {ex.Message}");
            }

            return dt;
        }

        private void MostrarEstadisticasBD()
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Base de datos no disponible.", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string estadisticas = "=== ESTADÍSTICAS DE BASE DE DATOS SQLITE ===\n\n";

                    // Carros por color
                    string query1 = @"SELECT ColorVehiculo, COUNT(*) as Total 
                                     FROM RegistroEventos 
                                     WHERE TipoEvento = 'Carro' AND ColorVehiculo != ''
                                     GROUP BY ColorVehiculo";

                    using (SQLiteCommand cmd = new SQLiteCommand(query1, conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            estadisticas += "CARROS POR COLOR:\n";
                            while (reader.Read())
                            {
                                estadisticas += $"• {reader["ColorVehiculo"]}: {reader["Total"]} carros\n";
                            }
                        }
                    }

                    estadisticas += "\n";

                    // Total de eventos
                    string query2 = "SELECT TipoEvento, COUNT(*) as Total FROM RegistroEventos GROUP BY TipoEvento";

                    using (SQLiteCommand cmd = new SQLiteCommand(query2, conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            estadisticas += "TOTALES POR TIPO:\n";
                            while (reader.Read())
                            {
                                estadisticas += $"• {reader["TipoEvento"]}: {reader["Total"]} eventos\n";
                            }
                        }
                    }

                    MessageBox.Show(estadisticas, "Estadísticas SQLite",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al obtener estadísticas: {ex.Message}");
            }
        }

        #endregion

        private void CrearInterfaz()
        {
            // Panel de información en tiempo real (arriba)
            GroupBox gbInfoTiempoReal = new GroupBox();
            gbInfoTiempoReal.Text = "INFORMACIÓN EN TIEMPO REAL";
            gbInfoTiempoReal.Location = new Point(10, 10);
            gbInfoTiempoReal.Size = new Size(860, 80);
            gbInfoTiempoReal.Font = new Font("Arial", 10, FontStyle.Bold);
            gbInfoTiempoReal.BackColor = Color.White;
            this.Controls.Add(gbInfoTiempoReal);

            // Label para información en tiempo real
            Label lblInfoTiempoReal = new Label();
            lblInfoTiempoReal.Name = "lblInfoTiempoReal";
            lblInfoTiempoReal.Location = new Point(10, 20);
            lblInfoTiempoReal.Size = new Size(840, 50);
            lblInfoTiempoReal.Font = new Font("Consolas", 11);
            lblInfoTiempoReal.Text = "Sistema listo con SQLite - Presiona INICIAR SIMULACIÓN";
            lblInfoTiempoReal.ForeColor = Color.DarkBlue;
            gbInfoTiempoReal.Controls.Add(lblInfoTiempoReal);

            // Panel de la calle mejorado
            PictureBox pbCalle = new PictureBox();
            pbCalle.Name = "pbCalle";
            pbCalle.Location = new Point(10, 100);
            pbCalle.Size = new Size(860, 350);
            pbCalle.BackColor = Color.FromArgb(135, 206, 250);
            pbCalle.BorderStyle = BorderStyle.FixedSingle;
            pbCalle.Paint += PbCalle_Paint;
            this.Controls.Add(pbCalle);

            // Panel de control
            GroupBox gbControl = new GroupBox();
            gbControl.Text = "PANEL DE CONTROL";
            gbControl.Location = new Point(10, 460);
            gbControl.Size = new Size(860, 100);
            gbControl.Font = new Font("Arial", 11, FontStyle.Bold);
            gbControl.BackColor = Color.White;
            this.Controls.Add(gbControl);

            // Puerto COM
            Label lblPuerto = new Label();
            lblPuerto.Text = "Puerto:";
            lblPuerto.Location = new Point(20, 30);
            lblPuerto.Size = new Size(60, 20);
            gbControl.Controls.Add(lblPuerto);

            ComboBox cbPuertos = new ComboBox();
            cbPuertos.Name = "cbPuertos";
            cbPuertos.Location = new Point(85, 28);
            cbPuertos.Size = new Size(90, 25);
            cbPuertos.DropDownStyle = ComboBoxStyle.DropDownList;
            gbControl.Controls.Add(cbPuertos);

            // Botón conectar
            Button btnConectar = new Button();
            btnConectar.Text = "CONECTAR";
            btnConectar.Location = new Point(185, 25);
            btnConectar.Size = new Size(120, 30);
            btnConectar.BackColor = Color.FromArgb(46, 204, 113);
            btnConectar.ForeColor = Color.White;
            btnConectar.FlatStyle = FlatStyle.Flat;
            btnConectar.Click += BtnConectar_Click;
            gbControl.Controls.Add(btnConectar);

            // Estado conexión
            Label lblEstado = new Label();
            lblEstado.Name = "lblEstado";
            lblEstado.Text = "⚫ DESCONECTADO";
            lblEstado.Location = new Point(315, 30);
            lblEstado.Size = new Size(200, 20);
            lblEstado.ForeColor = Color.Red;
            lblEstado.Font = new Font("Arial", 10, FontStyle.Bold);
            gbControl.Controls.Add(lblEstado);

            // Botón iniciar simulación
            Button btnIniciar = new Button();
            btnIniciar.Name = "btnIniciar";
            btnIniciar.Text = "▶ INICIAR SIMULACIÓN";
            btnIniciar.Location = new Point(20, 60);
            btnIniciar.Size = new Size(180, 35);
            btnIniciar.BackColor = Color.FromArgb(52, 152, 219);
            btnIniciar.ForeColor = Color.White;
            btnIniciar.Font = new Font("Arial", 11, FontStyle.Bold);
            btnIniciar.FlatStyle = FlatStyle.Flat;
            btnIniciar.Click += BtnIniciar_Click;
            gbControl.Controls.Add(btnIniciar);

            // Botón cambiar semáforo
            Button btnCambiar = new Button();
            btnCambiar.Name = "btnCambiar";
            btnCambiar.Text = "🚦 CAMBIAR SEMÁFORO";
            btnCambiar.Location = new Point(210, 60);
            btnCambiar.Size = new Size(200, 35);
            btnCambiar.BackColor = Color.FromArgb(230, 126, 34);
            btnCambiar.ForeColor = Color.White;
            btnCambiar.Font = new Font("Arial", 11, FontStyle.Bold);
            btnCambiar.FlatStyle = FlatStyle.Flat;
            btnCambiar.Enabled = false;
            btnCambiar.Click += BtnCambiar_Click;
            gbControl.Controls.Add(btnCambiar);

            // Botón terminar simulación
            Button btnTerminar = new Button();
            btnTerminar.Name = "btnTerminar";
            btnTerminar.Text = "⏹ TERMINAR SIMULACIÓN";
            btnTerminar.Location = new Point(420, 60);
            btnTerminar.Size = new Size(200, 35);
            btnTerminar.BackColor = Color.FromArgb(231, 76, 60);
            btnTerminar.ForeColor = Color.White;
            btnTerminar.Font = new Font("Arial", 11, FontStyle.Bold);
            btnTerminar.FlatStyle = FlatStyle.Flat;
            btnTerminar.Enabled = false;
            btnTerminar.Click += BtnTerminar_Click;
            gbControl.Controls.Add(btnTerminar);

            // Estado del semáforo
            Label lblEstadoSemaforo = new Label();
            lblEstadoSemaforo.Name = "lblEstadoSemaforo";
            lblEstadoSemaforo.Text = "SEMÁFORO: ROJO";
            lblEstadoSemaforo.Location = new Point(630, 65);
            lblEstadoSemaforo.Size = new Size(200, 25);
            lblEstadoSemaforo.Font = new Font("Arial", 14, FontStyle.Bold);
            lblEstadoSemaforo.ForeColor = Color.Red;
            gbControl.Controls.Add(lblEstadoSemaforo);

            /* Indicador de botón físico
            Label lblBotonFisico = new Label();
            lblBotonFisico.Name = "lblBotonFisico";
            lblBotonFisico.Text = "Botón Arduino: NO PRESIONADO";
            lblBotonFisico.Location = new Point(530, 30);
            lblBotonFisico.Size = new Size(280, 20);
            lblBotonFisico.Font = new Font("Arial", 9);
            lblBotonFisico.ForeColor = Color.Gray;
            gbControl.Controls.Add(lblBotonFisico);*/

            // Indicador de tecla del teclado matricial
            Label lblTeclaMatricial = new Label();
            lblTeclaMatricial.Name = "lblTeclaMatricial";
            lblTeclaMatricial.Text = "Tecla Matricial: ---";
            lblTeclaMatricial.Location = new Point(530, 30);  // Justo debajo del lblBotonFisico
            lblTeclaMatricial.Size = new Size(280, 20);
            lblTeclaMatricial.Font = new Font("Arial", 9);
            lblTeclaMatricial.ForeColor = Color.Purple;
            gbControl.Controls.Add(lblTeclaMatricial);

            // Panel de registro de eventos
            GroupBox gbRegistro = new GroupBox();
            gbRegistro.Text = "REGISTRO DE EVENTOS";
            gbRegistro.Location = new Point(10, 570);
            gbRegistro.Size = new Size(860, 120);
            gbRegistro.Font = new Font("Arial", 10, FontStyle.Bold);
            gbRegistro.BackColor = Color.White;
            this.Controls.Add(gbRegistro);

            // TextBox para eventos
            TextBox txtEventos = new TextBox();
            txtEventos.Name = "txtEventos";
            txtEventos.Location = new Point(10, 25);
            txtEventos.Size = new Size(600, 85);
            txtEventos.Multiline = true;
            txtEventos.ScrollBars = ScrollBars.Vertical;
            txtEventos.ReadOnly = true;
            txtEventos.Font = new Font("Consolas", 9);
            txtEventos.BackColor = Color.FromArgb(250, 250, 250);
            gbRegistro.Controls.Add(txtEventos);

            // Botón Ver Base de Datos
            Button btnVerBD = new Button();
            btnVerBD.Text = "📊 Ver Base de Datos SQLite";
            btnVerBD.Location = new Point(620, 25);
            btnVerBD.Size = new Size(230, 35);
            btnVerBD.BackColor = Color.FromArgb(46, 204, 113);
            btnVerBD.ForeColor = Color.White;
            btnVerBD.FlatStyle = FlatStyle.Flat;
            btnVerBD.Font = new Font("Arial", 10, FontStyle.Bold);
            btnVerBD.Click += BtnVerBD_Click;
            gbRegistro.Controls.Add(btnVerBD);

            // Botón Ver Estadísticas
            Button btnEstadisticas = new Button();
            btnEstadisticas.Name = "btnEstadisticas";
            btnEstadisticas.Text = "📈 Ver Estadísticas SQLite";
            btnEstadisticas.Location = new Point(620, 65);
            btnEstadisticas.Size = new Size(230, 35);
            btnEstadisticas.BackColor = Color.FromArgb(52, 152, 219);
            btnEstadisticas.ForeColor = Color.White;
            btnEstadisticas.FlatStyle = FlatStyle.Flat;
            btnEstadisticas.Font = new Font("Arial", 10, FontStyle.Bold);
            btnEstadisticas.Click += BtnEstadisticas_Click;
            gbRegistro.Controls.Add(btnEstadisticas);

            ActualizarPuertos();
        }

        private void ActualizarPuertos()
        {
            ComboBox cb = (ComboBox)this.Controls.Find("cbPuertos", true)[0];
            if (cb != null)
            {
                cb.Items.Clear();
                cb.Items.Add("COM5");

                string[] puertos = SerialPort.GetPortNames();
                foreach (string puerto in puertos)
                {
                    if (!cb.Items.Contains(puerto))
                        cb.Items.Add(puerto);
                }

                if (cb.Items.Contains("COM5"))
                    cb.SelectedItem = "COM5";
                else if (cb.Items.Count > 0)
                    cb.SelectedIndex = 0;
            }
        }

        private void BtnConectar_Click(object sender, EventArgs e)
        {
            try
            {
                ComboBox cb = (ComboBox)this.Controls.Find("cbPuertos", true)[0];
                if (cb?.SelectedItem == null)
                {
                    MessageBox.Show("Selecciona un puerto COM", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (arduino.IsOpen)
                    arduino.Close();

                arduino.PortName = cb.SelectedItem.ToString();
                arduino.Open();

                Label lblEstado = (Label)this.Controls.Find("lblEstado", true)[0];
                if (lblEstado != null)
                {
                    lblEstado.Text = "🟢 CONECTADO";
                    lblEstado.ForeColor = Color.Green;
                }

                arduino.WriteLine("ROJO");

                MessageBox.Show("Arduino conectado correctamente!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnIniciar_Click(object sender, EventArgs e)
        {
            if (!arduino.IsOpen)
            {
                MessageBox.Show("Primero conecta el Arduino!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Iniciar simulación
            simulacionActiva = true;
            tiempoInicioSimulacion = DateTime.Now;
            tiempoInicioEstado = DateTime.Now;
            estadoSemaforo = "VERDE";
            posicionCarro = -80;
            velocidadCarro = 5;

            // Resetear contadores
            contadorCarros = 0;
            contadorPeatones = 0;
            contadorCiclos = 0;
            foreach (var key in contadorPorColor.Keys.ToList())
            {
                contadorPorColor[key] = 0;
            }

            // Asignar color aleatorio al primer carro
            AsignarColorAleatorio();

            // Habilitar/deshabilitar botones
            Button btnCambiar = (Button)this.Controls.Find("btnCambiar", true)[0];
            if (btnCambiar != null) btnCambiar.Enabled = true;

            Button btnTerminar = (Button)this.Controls.Find("btnTerminar", true)[0];
            if (btnTerminar != null) btnTerminar.Enabled = true;

            Button btnIniciar = (Button)this.Controls.Find("btnIniciar", true)[0];
            if (btnIniciar != null) btnIniciar.Enabled = false;

            CambiarSemaforo("VERDE");
            RegistrarEvento("SIMULACIÓN INICIADA - Semáforo en VERDE");
            ActualizarInfoTiempoReal();
        }

        private void ProbarLecturaBD()
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM RegistroEventos";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        int total = Convert.ToInt32(cmd.ExecuteScalar());
                        MessageBox.Show($"Total de eventos en BD: {total}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al acceder a SQLite: " + ex.Message);
            }
        }
        private void BtnTerminar_Click(object sender, EventArgs e)
        {
            ProbarLecturaBD();
            GuardarResumenFinal(); // Guarda todos los datos generados en la simulación
            FinalizarSimulacion(); // Detiene animaciones, botones, etc.
            RegistrarEvento("✓ Simulación finalizada y resumen guardado");

            // Validación extra para que se detenga la animación y melodía al dar clic en TERMINAR
            try
            {
                if (arduino != null && arduino.IsOpen)
                {
                    arduino.WriteLine("APAGAR");
                    arduino.Close();
                }

                if (timerAnimacion != null)
                {
                    timerAnimacion.Stop();
                    timerAnimacion.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cerrar: {ex.Message}");
            }
        }

        private void BtnCambiar_Click(object sender, EventArgs e)
        {
            if (!simulacionActiva) return;

            // Guardar tiempo del estado anterior
            TimeSpan tiempoEstado = DateTime.Now - tiempoInicioEstado;
            if (tiemposPorEstado.ContainsKey(estadoSemaforo))
                tiemposPorEstado[estadoSemaforo] = tiempoEstado;
            else
                tiemposPorEstado.Add(estadoSemaforo, tiempoEstado);

            // Cambiar al siguiente estado
            switch (estadoSemaforo)
            {
                case "VERDE":
                    estadoSemaforo = "AMARILLO";
                    velocidadCarro = 3;
                    RegistrarEvento($"Cambio a AMARILLO - Tiempo en verde: {tiempoEstado.TotalSeconds:F1}s");
                    break;

                case "AMARILLO":
                    estadoSemaforo = "ROJO";
                    velocidadCarro = 0;
                    peatonCruzando = true;
                    RegistrarEvento($"Cambio a ROJO - Tiempo en amarillo: {tiempoEstado.TotalSeconds:F1}s");
                    RegistrarEvento("PEATÓN INICIANDO CRUCE");
                    break;

                case "ROJO":
                    // Cambiar de nuevo a verde para continuar el ciclo
                    estadoSemaforo = "VERDE";
                    velocidadCarro = 5;
                    contadorCiclos++;
                    RegistrarEvento($"Cambio a VERDE - Ciclo #{contadorCiclos} completado");
                    RegistrarEvento($"RESUMEN: Carros pasados: {contadorCarros} | Peatones cruzados: {contadorPeatones}");
                    break;
            }

            tiempoInicioEstado = DateTime.Now;
            CambiarSemaforo(estadoSemaforo);
            ActualizarInfoTiempoReal();
        }

        private void Arduino_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string datos = arduino.ReadLine().Trim();

                this.Invoke(new Action(() =>
                {
                    if (datos.StartsWith("Tecla presionada: "))
                    {
                        string tecla = datos.Substring(6);
                        Console.WriteLine("Tecla presionada: " + tecla);

                        // Mostrar la tecla en el nuevo Label
                        Label lblTecla= (Label)this.Controls.Find("lblTeclaMatricial", true)[0];
                        if (lblTecla != null)
                        {
                            lblTecla.Text = $"Tecla matricial {tecla}";
                        }

                        switch (tecla)
                        {
                            case "presionada: 1":
                                Button btnIniciar = (Button)this.Controls.Find("btnIniciar", true)[0];
                                btnIniciar.PerformClick();
                                ReproducirSonido("start.wav");
                                break;
                            case "presionada: 2":
                                Button btnCambiar = (Button)this.Controls.Find("btnCambiar", true)[0];
                                btnCambiar.PerformClick();
                                ReproducirSonido("clic.wav");
                                break;
                            case "presionada: 3":
                                Button btnTerminar = (Button)this.Controls.Find("btnTerminar", true)[0];
                                btnTerminar.PerformClick();
                                ReproducirSonido("close.wav");
                                break;
                            case "presionada: A":
                                // Abrir la ventana de estadísticas
                                Button btnEstadisticas = (Button)this.Controls.Find("btnEstadisticas", true)[0];
                                btnEstadisticas.PerformClick();
                                ReproducirSonido("data.wav");
                                break;
                            default:
                                break;
                        }
                    }
                    /*if (datos == "BOTON_PRESIONADO" && simulacionActiva)
                    {
                        Label lblBoton = (Label)this.Controls.Find("lblBotonFisico", true)[0];
                        if (lblBoton != null)
                        {
                            lblBoton.Text = "Botón Arduino: PRESIONADO";
                            lblBoton.ForeColor = Color.Green;
                        }

                        // Cambiar semáforo
                        BtnCambiar_Click(null, null);

                        // Reset después de 500ms
                        System.Windows.Forms.Timer tReset = new System.Windows.Forms.Timer();
                        tReset.Interval = 500;
                        tReset.Tick += (s, args) =>
                        {
                            if (lblBoton != null)
                            {
                                lblBoton.Text = "Botón Arduino: NO PRESIONADO";
                                lblBoton.ForeColor = Color.Black;
                            }
                            tReset.Stop();
                            tReset.Dispose();
                        };
                        tReset.Start();
                    }*/
                }));
            }
            catch { }
        }

        private void CambiarSemaforo(string nuevoEstado)
        {
            if (arduino.IsOpen)
            {
                arduino.WriteLine(nuevoEstado);
            }

            // Actualizar label
            Label lblEstadoSem = (Label)this.Controls.Find("lblEstadoSemaforo", true)[0];
            if (lblEstadoSem != null)
            {
                lblEstadoSem.Text = "SEMÁFORO: " + nuevoEstado;

                switch (nuevoEstado)
                {
                    case "VERDE":
                        lblEstadoSem.ForeColor = Color.Green;
                        break;
                    case "AMARILLO":
                        lblEstadoSem.ForeColor = Color.Orange;
                        break;
                    case "ROJO":
                        lblEstadoSem.ForeColor = Color.Red;
                        break;
                }
            }

            PictureBox pb = (PictureBox)this.Controls.Find("pbCalle", true)[0];
            pb?.Invalidate();
        }

        private void TimerAnimacion_Tick(object sender, EventArgs e)
        {
            // Animar peatón esperando
            if (!peatonCruzando)
            {
                peatonAnimacion = (peatonAnimacion + 1) % 20;
            }

            // Mover carro si la simulación está activa
            if (simulacionActiva && velocidadCarro > 0)
            {
                posicionCarro += velocidadCarro;
                if (posicionCarro > 900)
                {
                    // Contar carro que pasó
                    if (estadoSemaforo == "VERDE" || estadoSemaforo == "AMARILLO")
                    {
                        contadorCarros++;
                        string nombreColor = ObtenerNombreColor();
                        if (contadorPorColor.ContainsKey(nombreColor))
                            contadorPorColor[nombreColor]++;

                        RegistrarEvento($"Carro #{contadorCarros} ({nombreColor}) completó el recorrido");

                        // Guardar en base de datos SQLite
                        TimeSpan tiempoDesdeInicio = DateTime.Now - tiempoInicioSimulacion;
                        GuardarEventoEnBD("Carro", nombreColor, contadorCarros, estadoSemaforo,
                                         tiempoDesdeInicio.TotalSeconds, velocidadCarro * 10,
                                         "Carro completó recorrido");

                        // Asignar nuevo color aleatorio para el siguiente carro
                        AsignarColorAleatorio();
                    }
                    posicionCarro = -80;
                }
            }
            else if (simulacionActiva && estadoSemaforo == "ROJO")
            {
                // Si el carro ya pasó el semáforo (posición 470), puede continuar
                if (posicionCarro > 470)
                {
                    posicionCarro += 5; // Continúa a velocidad normal
                    if (posicionCarro > 900)
                    {
                        posicionCarro = -80;
                        AsignarColorAleatorio();
                    }
                }
                // Si no ha llegado al semáforo, se detiene antes del paso cebra
                else if (posicionCarro < 470 && posicionCarro > -80)
                {
                    posicionCarro += 6;
                    if (posicionCarro > 470)
                    {
                        posicionCarro = 470; // Detener exactamente antes del paso cebra
                    }
                }

                // Cuando el carro está detenido en rojo, apagar el display
                if (arduino.IsOpen && posicionCarro == 470)
                {
                    arduino.WriteLine("COLOR: ");
                }
            }

            // Mover peatón cuando cruza
            if (peatonCruzando && estadoSemaforo == "ROJO")
            {
                posicionPeatonY -= 3;
                if (posicionPeatonY <= 150) // Llegó al otro lado
                {
                    contadorPeatones++;
                    RegistrarEvento($"Peatón #{contadorPeatones} terminó de cruzar");

                    // Guardar en base de datos SQLite
                    TimeSpan tiempoDesdeInicio = DateTime.Now - tiempoInicioSimulacion;
                    GuardarEventoEnBD("Peatón", "", contadorPeatones, "ROJO",
                                     tiempoDesdeInicio.TotalSeconds, 0,
                                     "Peatón cruzó exitosamente");

                    // Resetear posición del peatón para el siguiente cruce
                    posicionPeatonY = 350;
                    peatonCruzando = false;
                }
            }

            // Actualizar información en tiempo real
            if (simulacionActiva)
            {
                ActualizarInfoTiempoReal();
            }

            PictureBox pb = (PictureBox)this.Controls.Find("pbCalle", true)[0];
            pb?.Invalidate();
        }

        private void ActualizarInfoTiempoReal()
        {
            Label lblInfo = (Label)this.Controls.Find("lblInfoTiempoReal", true)[0];
            if (lblInfo == null) return;

            TimeSpan tiempoActual = DateTime.Now - tiempoInicioEstado;
            TimeSpan tiempoTotal = DateTime.Now - tiempoInicioSimulacion;

            string info = $"Estado: {estadoSemaforo} | ";
            info += $"Tiempo en estado: {tiempoActual.TotalSeconds:F1}s | ";
            info += $"Tiempo total: {tiempoTotal.TotalMinutes:F1} min | ";

            switch (estadoSemaforo)
            {
                case "VERDE":
                    info += $"Velocidad: {velocidadCarro * 10} km/h | Carro en movimiento";
                    break;
                case "AMARILLO":
                    info += $"Velocidad: {velocidadCarro * 10} km/h | Carro reduciendo velocidad";
                    break;
                case "ROJO":
                    info += "Velocidad: ";
                    if (posicionCarro > 470)
                        info += "50 km/h | Carro pasó el semáforo";
                    else if (posicionCarro == 470)
                        info += "0 km/h | Carro detenido en semáforo";
                    else
                        info += "Reduciendo | Carro aproximándose";

                    if (peatonCruzando)
                        info += " - Peatón cruzando";
                    break;
            }

            info += $"\nCarros: {contadorCarros} | Peatones: {contadorPeatones} | Ciclos: {contadorCiclos} | BD: SQLite";

            lblInfo.Text = info;
        }

        private void FinalizarSimulacion()
        {
            simulacionActiva = false;

            // Calcular estadísticas finales
            TimeSpan tiempoTotal = DateTime.Now - tiempoInicioSimulacion;

            // Crear resumen detallado por colores
            string detalleColores = "\nDETALLE DE CARROS POR COLOR:\n";
            foreach (var kvp in contadorPorColor.OrderByDescending(x => x.Value))
            {
                if (kvp.Value > 0)
                {
                    detalleColores += $"• {kvp.Key}: {kvp.Value} carro(s)\n";
                }
            }

            // Mostrar resumen
            string resumen = "=== RESUMEN DE LA SIMULACIÓN ===\n";
            resumen += $"Tiempo total: {tiempoTotal.TotalMinutes:F1} minutos\n";
            resumen += $"Ciclos completados: {contadorCiclos}\n";
            resumen += $"\nTOTAL DE VEHÍCULOS: {contadorCarros}\n";
            resumen += detalleColores;
            resumen += $"\nTOTAL DE PEATONES: {contadorPeatones}\n";
            resumen += $"\nPROMEDIOS:\n";
            resumen += $"• {(contadorCarros > 0 ? tiempoTotal.TotalSeconds / contadorCarros : 0):F1} segundos por carro\n";
            resumen += $"• {(contadorPeatones > 0 ? tiempoTotal.TotalSeconds / contadorPeatones : 0):F1} segundos por peatón";
            resumen += $"\n\n✓ Datos guardados en base de datos SQLite";

            MessageBox.Show(resumen, "Resumen de Simulación", MessageBoxButtons.OK, MessageBoxIcon.Information);

            RegistrarEvento("=== SIMULACIÓN FINALIZADA ===");
            RegistrarEvento(resumen);

            // Deshabilitar botones
            Button btnCambiar = (Button)this.Controls.Find("btnCambiar", true)[0];
            if (btnCambiar != null) btnCambiar.Enabled = false;

            Button btnTerminar = (Button)this.Controls.Find("btnTerminar", true)[0];
            if (btnTerminar != null) btnTerminar.Enabled = false;

            // Habilitar botón iniciar
            Button btnIniciar = (Button)this.Controls.Find("btnIniciar", true)[0];
            if (btnIniciar != null) btnIniciar.Enabled = true;

            // Resetear semáforo a rojo
            CambiarSemaforo("ROJO");

            // Resetear posiciones
            posicionCarro = -80;
            posicionPeatonY = 350;
            peatonCruzando = false;
            velocidadCarro = 0;

            // Actualizar info final
            Label lblInfo = (Label)this.Controls.Find("lblInfoTiempoReal", true)[0];
            if (lblInfo != null)
                lblInfo.Text = $"Simulación finalizada - Total: {contadorCarros} carros, {contadorPeatones} peatones - Guardado en SQLite";
        }

        private void AsignarColorAleatorio()
        {
            int indice = random.Next(coloresDisponibles.Count);
            colorCarroActual = coloresDisponibles[indice];

            // Enviar el color al Arduino cuando el carro está en movimiento
            if (arduino.IsOpen && (estadoSemaforo == "VERDE" || estadoSemaforo == "AMARILLO"))
            {
                string nombreColor = nombresColores[indice];
                arduino.WriteLine($"COLOR:{nombreColor}");
            }
        }

        private string ObtenerNombreColor()
        {
            // Buscar el nombre del color actual
            for (int i = 0; i < coloresDisponibles.Count; i++)
            {
                if (coloresDisponibles[i].ToArgb() == colorCarroActual.ToArgb())
                {
                    return nombresColores[i];
                }
            }
            return "Desconocido";
        }

        private void PbCalle_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fondo - Cielo con gradiente
            LinearGradientBrush gradienteCielo = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, 150),
                Color.FromArgb(135, 206, 250),
                Color.FromArgb(255, 255, 255)
            );
            g.FillRectangle(gradienteCielo, 0, 0, 860, 150);

            // Edificios de fondo
            DrawEdificios(g);

            // Acera superior
            g.FillRectangle(new SolidBrush(Color.FromArgb(180, 180, 180)), 0, 150, 860, 50);
            DrawTexturaAcera(g, 0, 150, 860, 50);

            // Calle principal
            g.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 50)), 0, 200, 860, 100);

            // Líneas amarillas de la calle
            Pen penLineaAmarilla = new Pen(Color.Yellow, 4);
            penLineaAmarilla.DashStyle = DashStyle.Dash;
            g.DrawLine(penLineaAmarilla, 0, 250, 860, 250);

            // Acera inferior
            g.FillRectangle(new SolidBrush(Color.FromArgb(180, 180, 180)), 0, 300, 860, 50);
            DrawTexturaAcera(g, 0, 300, 860, 50);

            // Paso peatonal mejorado
            for (int i = 0; i < 10; i++)
            {
                g.FillRectangle(Brushes.White, 500 + (i * 20), 200, 15, 100);
            }

            // Semáforo mejorado con sombra
            DrawSemaforoCompleto(g, 470, 80);

            // Dibujar carro mejorado
            if (simulacionActiva || posicionCarro > -80)
            {
                DrawCarroMejorado(g, posicionCarro, 230);
            }

            // Dibujar peatón siempre visible
            DrawPeatonAnimado(g, 550, posicionPeatonY);

            // Señal de peatón mejorada
            DrawSenalPeaton(g, 650, 120);
        }

        private void DrawEdificios(Graphics g)
        {
            // Edificios de fondo para dar profundidad
            g.FillRectangle(new SolidBrush(Color.FromArgb(150, 150, 150)), 50, 50, 100, 100);
            g.FillRectangle(new SolidBrush(Color.FromArgb(170, 170, 170)), 200, 30, 120, 120);
            g.FillRectangle(new SolidBrush(Color.FromArgb(160, 160, 160)), 600, 40, 110, 110);
            g.FillRectangle(new SolidBrush(Color.FromArgb(140, 140, 140)), 750, 60, 90, 90);

            // Ventanas
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    g.FillRectangle(Brushes.LightYellow, 60 + j * 20, 60 + i * 25, 15, 20);
                    g.FillRectangle(Brushes.LightBlue, 210 + j * 25, 40 + i * 30, 20, 25);
                }
            }
        }

        private void DrawTexturaAcera(Graphics g, int x, int y, int width, int height)
        {
            // Agregar textura a la acera
            Random r = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                int rx = r.Next(x, x + width);
                int ry = r.Next(y, y + height);
                g.FillEllipse(new SolidBrush(Color.FromArgb(30, 0, 0, 0)), rx, ry, 3, 3);
            }
        }

        private void DrawSemaforoCompleto(Graphics g, int x, int y)
        {
            // Sombra del semáforo
            g.FillEllipse(new SolidBrush(Color.FromArgb(50, 0, 0, 0)), x - 5, y + 140, 50, 20);

            // Poste
            LinearGradientBrush brushPoste = new LinearGradientBrush(
                new Point(x + 15, y + 70),
                new Point(x + 25, y + 70),
                Color.FromArgb(100, 100, 100),
                Color.FromArgb(60, 60, 60)
            );
            g.FillRectangle(brushPoste, x + 15, y + 70, 10, 80);

            // Caja del semáforo con gradiente
            LinearGradientBrush brushCaja = new LinearGradientBrush(
                new Point(x, y),
                new Point(x + 40, y),
                Color.FromArgb(40, 40, 40),
                Color.Black
            );
            g.FillRectangle(brushCaja, x, y, 40, 80);
            g.DrawRectangle(new Pen(Color.FromArgb(80, 80, 80), 2), x, y, 40, 80);

            // Viseras
            g.FillRectangle(Brushes.Black, x - 5, y - 5, 50, 10);
            g.FillRectangle(Brushes.Black, x - 5, y + 25, 50, 5);
            g.FillRectangle(Brushes.Black, x - 5, y + 50, 50, 5);
            g.FillRectangle(Brushes.Black, x - 5, y + 75, 50, 5);

            // Luces del semáforo
            DrawSemaforoLuz(g, x + 20, y + 15, estadoSemaforo == "ROJO", Color.Red);
            DrawSemaforoLuz(g, x + 20, y + 40, estadoSemaforo == "AMARILLO", Color.Yellow);
            DrawSemaforoLuz(g, x + 20, y + 65, estadoSemaforo == "VERDE", Color.Green);
        }

        private void DrawSemaforoLuz(Graphics g, int x, int y, bool encendida, Color color)
        {
            if (encendida)
            {
                // Efecto de brillo exterior
                for (int i = 3; i > 0; i--)
                {
                    Color glowColor = Color.FromArgb(30 * i, color);
                    g.FillEllipse(new SolidBrush(glowColor), x - 12 - i * 2, y - 12 - i * 2, 24 + i * 4, 24 + i * 4);
                }

                // Luz principal con gradiente - CORREGIDO
                try
                {
                    PathGradientBrush brush = new PathGradientBrush(
                        new Point[] {
                            new Point(x - 12, y),
                            new Point(x + 12, y),
                            new Point(x, y - 12),
                            new Point(x, y + 12)
                        }
                    );
                    brush.CenterColor = Color.White;
                    brush.SurroundColors = new Color[] { color, color, color, color }; // ARRAY CORREGIDO
                    g.FillEllipse(brush, x - 12, y - 12, 24, 24);
                    brush.Dispose();
                }
                catch
                {
                    // Fallback si hay error con el gradiente
                    g.FillEllipse(new SolidBrush(color), x - 12, y - 12, 24, 24);
                }
            }
            else
            {
                // Luz apagada
                Color colorApagado = Color.FromArgb(80, color.R / 4, color.G / 4, color.B / 4);
                g.FillEllipse(new SolidBrush(colorApagado), x - 12, y - 12, 24, 24);
            }

            // Borde de la luz
            g.DrawEllipse(new Pen(Color.FromArgb(30, 30, 30), 2), x - 12, y - 12, 24, 24);
        }

        private void DrawCarroMejorado(Graphics g, int x, int y)
        {
            // Sombra del carro
            g.FillEllipse(new SolidBrush(Color.FromArgb(60, 0, 0, 0)), x - 5, y + 35, 80, 20);

            // Carrocería principal con el color actual
            LinearGradientBrush brushCarro = new LinearGradientBrush(
                new Point(x, y),
                new Point(x, y + 35),
                colorCarroActual,
                Color.FromArgb(colorCarroActual.R / 2, colorCarroActual.G / 2, colorCarroActual.B / 2)
            );

            // Base del carro
            GraphicsPath pathCarro = new GraphicsPath();
            pathCarro.AddLines(new Point[] {
                new Point(x, y + 20),
                new Point(x + 5, y + 15),
                new Point(x + 15, y + 5),
                new Point(x + 45, y + 5),
                new Point(x + 55, y + 15),
                new Point(x + 60, y + 20),
                new Point(x + 60, y + 30),
                new Point(x, y + 30)
            });
            pathCarro.CloseFigure();
            g.FillPath(brushCarro, pathCarro);

            // Contorno del carro
            g.DrawPath(new Pen(Color.Black, 1), pathCarro);

            // Ventanas con reflejo
            LinearGradientBrush brushVentana = new LinearGradientBrush(
                new Point(x + 20, y + 8),
                new Point(x + 20, y + 18),
                Color.FromArgb(180, 200, 230),
                Color.FromArgb(120, 150, 200)
            );
            g.FillRectangle(brushVentana, x + 20, y + 8, 20, 10);
            g.DrawRectangle(Pens.Black, x + 20, y + 8, 20, 10);

            // Ruedas con detalle
            g.FillEllipse(Brushes.Black, x + 8, y + 28, 14, 14);
            g.FillEllipse(new SolidBrush(Color.FromArgb(80, 80, 80)), x + 10, y + 30, 10, 10);
            g.FillEllipse(Brushes.Silver, x + 12, y + 32, 6, 6);

            g.FillEllipse(Brushes.Black, x + 38, y + 28, 14, 14);
            g.FillEllipse(new SolidBrush(Color.FromArgb(80, 80, 80)), x + 40, y + 30, 10, 10);
            g.FillEllipse(Brushes.Silver, x + 42, y + 32, 6, 6);

            // Faros delanteros
            if (colorCarroActual.R < 100 && colorCarroActual.G < 100 && colorCarroActual.B < 100)
            {
                g.FillEllipse(new SolidBrush(Color.FromArgb(255, 255, 255, 200)), x + 58, y + 18, 8, 6);
            }
            else
            {
                g.FillEllipse(new SolidBrush(Color.FromArgb(200, 255, 255, 200)), x + 58, y + 18, 8, 6);
            }

            // Luces traseras
            g.FillEllipse(new SolidBrush(Color.FromArgb(200, 255, 0, 0)), x - 2, y + 18, 6, 4);

            // Placa
            g.FillRectangle(Brushes.White, x + 25, y + 25, 10, 4);
            g.DrawRectangle(Pens.Black, x + 25, y + 25, 10, 4);
        }

        private void DrawPeatonAnimado(Graphics g, int x, int y)
        {
            // Ajustar posición según animación
            int offsetX = 0;
            if (!peatonCruzando && peatonAnimacion < 10)
            {
                offsetX = peatonAnimacion / 2 - 2;
            }

            x += offsetX;

            // Sombra
            g.FillEllipse(new SolidBrush(Color.FromArgb(40, 0, 0, 0)), x - 10, y + 35, 30, 10);

            // Cabeza con detalles
            g.FillEllipse(new SolidBrush(Color.FromArgb(255, 220, 177)), x, y, 20, 20);
            g.DrawEllipse(new Pen(Color.FromArgb(200, 180, 140), 1), x, y, 20, 20);

            // Cabello
            g.FillEllipse(Brushes.Black, x + 2, y - 2, 16, 8);

            // Ojos
            g.FillEllipse(Brushes.Black, x + 5, y + 7, 3, 3);
            g.FillEllipse(Brushes.Black, x + 12, y + 7, 3, 3);

            // Boca
            if (!peatonCruzando)
            {
                g.DrawArc(new Pen(Color.Black, 1), x + 5, y + 10, 10, 5, 0, 180);
            }
            else
            {
                g.DrawLine(new Pen(Color.Black, 1), x + 7, y + 13, x + 13, y + 13);
            }

            // Cuerpo - camisa
            LinearGradientBrush brushCamisa = new LinearGradientBrush(
                new Point(x + 5, y + 20),
                new Point(x + 5, y + 40),
                Color.FromArgb(0, 120, 215),
                Color.FromArgb(0, 80, 150)
            );
            g.FillRectangle(brushCamisa, x + 3, y + 20, 14, 20);

            // Pantalón
            g.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 100)), x + 3, y + 40, 14, 15);

            // Brazos
            int brazoOffset = 0;
            if (!peatonCruzando && peatonAnimacion > 10)
            {
                brazoOffset = 2;
            }

            g.DrawLine(new Pen(Color.FromArgb(255, 220, 177), 3), x + 3, y + 25, x - 5 + brazoOffset, y + 35);
            g.DrawLine(new Pen(Color.FromArgb(255, 220, 177), 3), x + 17, y + 25, x + 25 - brazoOffset, y + 35);

            // Piernas
            if (peatonCruzando)
            {
                int paso = (int)(posicionPeatonY % 20);
                g.DrawLine(new Pen(Color.Black, 3), x + 8, y + 55, x + 5 + paso / 4, y + 70);
                g.DrawLine(new Pen(Color.Black, 3), x + 12, y + 55, x + 15 - paso / 4, y + 70);
            }
            else
            {
                g.DrawLine(new Pen(Color.Black, 3), x + 8, y + 55, x + 6, y + 70);
                g.DrawLine(new Pen(Color.Black, 3), x + 12, y + 55, x + 14, y + 70);
            }

            // Zapatos
            g.FillEllipse(Brushes.Black, x + 3, y + 68, 8, 5);
            g.FillEllipse(Brushes.Black, x + 11, y + 68, 8, 5);
        }

        private void DrawSenalPeaton(Graphics g, int x, int y)
        {
            // Marco de la señal
            g.FillRectangle(Brushes.Black, x, y, 40, 50);
            g.DrawRectangle(new Pen(Color.Gray, 2), x, y, 40, 50);

            // Luz superior
            if (estadoSemaforo == "ROJO")
            {
                g.FillEllipse(new SolidBrush(Color.LimeGreen), x + 10, y + 5, 20, 20);
                g.DrawString("🚶", new Font("Arial", 12), Brushes.Black, x + 12, y + 7);
            }
            else
            {
                g.FillEllipse(new SolidBrush(Color.Red), x + 10, y + 5, 20, 20);
                g.DrawString("✋", new Font("Arial", 12), Brushes.White, x + 12, y + 7);
            }

            // Contador
            if (estadoSemaforo == "ROJO" && peatonCruzando)
            {
                TimeSpan tiempo = DateTime.Now - tiempoInicioEstado;
                int segundosRestantes = Math.Max(0, 5 - (int)tiempo.TotalSeconds);
                g.DrawString(segundosRestantes.ToString(), new Font("Arial", 14, FontStyle.Bold),
                    Brushes.Yellow, x + 13, y + 28);
            }
        }

        private void RegistrarEvento(string evento)
        {
            string eventoConHora = $"[{DateTime.Now:HH:mm:ss}] {evento}";
            registroEventos.Add(eventoConHora);

            Control[] encontrados = this.Controls.Find("txtEventos", true);
            if (encontrados.Length > 0 && encontrados[0] is TextBox txtEventos)
            {
                txtEventos.AppendText(eventoConHora + Environment.NewLine);
                txtEventos.ScrollToCaret();
            }
        }

        private void BtnVerBD_Click(object sender, EventArgs e)
        {
            try
            {
                // Crear un nuevo formulario para mostrar los datos
                Form frmDatos = new Form();
                frmDatos.Text = "Registros de Base de Datos SQLite";
                frmDatos.Size = new Size(1000, 600);
                frmDatos.StartPosition = FormStartPosition.CenterScreen;

                // Crear DataGridView
                DataGridView dgv = new DataGridView();
                dgv.Dock = DockStyle.Fill;
                dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dgv.ReadOnly = true;
                dgv.AllowUserToAddRows = false;
                dgv.AllowUserToDeleteRows = false;

                // Cargar datos
                dgv.DataSource = ObtenerRegistrosBD();

                // Agregar al formulario
                frmDatos.Controls.Add(dgv);

                // Mostrar formulario
                frmDatos.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mostrar datos: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEstadisticas_Click(object sender, EventArgs e)
        {
            //MostrarEstadisticasBD();
            FormEstadisticas ventana = new FormEstadisticas();
            ventana.ShowDialog(); // Muestra la ventana como modal
        }

        private void ReproducirSonido(string nombreArchivo)
        {
            try
            {
                string ruta = Path.Combine(Application.StartupPath, "Sonidos", nombreArchivo);
                if (File.Exists(ruta))
                {
                    SoundPlayer player = new SoundPlayer(ruta);
                    player.Play(); // Usa .PlaySync() si quieres que espere a que termine
                }
                else
                {
                    Console.WriteLine("No se encontró el archivo de sonido: " + ruta);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al reproducir sonido: " + ex.Message);
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (arduino != null && arduino.IsOpen)
                {
                    arduino.WriteLine("APAGAR");
                    arduino.Close();
                }

                if (timerAnimacion != null)
                {
                    timerAnimacion.Stop();
                    timerAnimacion.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cerrar: {ex.Message}");
            }

            base.OnFormClosing(e);
        }

        // Método adicional para evitar errores del Form_Load
        private void Form1_Load(object sender, EventArgs e)
        {
            // Este método se ejecuta cuando se carga el formulario
            // No es necesario código aquí ya que todo se inicializa en el constructor
        }
    }
}