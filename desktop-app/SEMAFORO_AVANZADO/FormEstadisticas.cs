using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.SQLite;

namespace SEMAFORO_AVANZADO
{
    public partial class FormEstadisticas : Form
    {
        public FormEstadisticas()
        {
            InitializeComponent();
            CargarGraficas(); // 👈 Aquí se llama al método al iniciar el formulario
        }
        /* Conexión a la BDD */
        private string dbPath = "SemaforoDB.sqlite";
        private string connectionString => $"Data Source={dbPath};Version=3;";
        /* Código de conexión para la BDD si no está el archivo .sqlite en donde está el .exe
        private string dbPath = Path.Combine(Application.StartupPath, "SemaforoDB.sqlite");
        private string connectionString => $"Data Source={dbPath};Version=3;";
        */

        private void CargarGraficas()
        {
            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                // === GRÁFICO 1: Vehículos por color ===
                string queryColores = @"
                SELECT 
                    LOWER(ColorVehiculo) AS ColorNormalizado, 
                    COUNT(*) AS Total
                FROM RegistroEventos
                WHERE TipoEvento = 'Carro'
                GROUP BY ColorNormalizado";

                chart1.Series.Clear();
                chart1.Titles.Clear();
                chart1.Titles.Add("Vehículos por Color");
                chart1.Palette = ChartColorPalette.Bright;

                // Limpieza visual
                chart1.ChartAreas[0].AxisX.Title = "Color";
                chart1.ChartAreas[0].AxisY.Title = "Cantidad";
                //chart1.ChartAreas[0].AxisX.LabelStyle.Angle = -45;
                chart1.ChartAreas[0].AxisX.Interval = 1;
                chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
                chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;

                // Añadir márgenes visibles
                chart1.ChartAreas[0].AxisX.IsMarginVisible = true;

                // Mejorar ajuste del gráfico
                chart1.ChartAreas[0].InnerPlotPosition = new ElementPosition(15, 5, 70, 85); // más aire inferior

                // Configurar serie
                Series serieColores = new Series("Vehículos por Color")
                {
                    ChartType = SeriesChartType.Column,
                    Font = new Font("Arial", 9),
                    IsValueShownAsLabel = true,
                    ["PointWidth"] = "0.4",
                    ["PixelPointWidth"] = "30",
                    ["DrawingStyle"] = "Cylinder"
                };

                using (var cmd = new SQLiteCommand(queryColores, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string rawColor = reader["ColorNormalizado"].ToString();
                        string color = char.ToUpper(rawColor[0]) + rawColor.Substring(1);
                        int total = Convert.ToInt32(reader["Total"]);
                        serieColores.Points.AddXY(color, total);
                    }
                }

                chart1.Series.Add(serieColores);

                // === GRÁFICO 2: Tipos de evento ===
                string queryTipos = @"
    SELECT TipoEvento, COUNT(*) as Total
    FROM RegistroEventos
    GROUP BY TipoEvento";

                chart2.Series.Clear();
                chart2.Titles.Clear();
                chart2.Titles.Add("Tipos de Eventos Registrados");

                // Estilo general
                chart2.BackColor = Color.White;
                chart2.Titles[0].Font = new Font("Arial", 12, FontStyle.Bold);
                chart2.Palette = ChartColorPalette.BrightPastel;

                Series serieTipos = new Series("Tipos de Evento")
                {
                    ChartType = SeriesChartType.Pie,
                    Font = new Font("Arial", 9),
                    IsValueShownAsLabel = true,
                    LabelForeColor = Color.Black
                };

                // Detalles visuales extra
                serieTipos["PieLabelStyle"] = "Outside"; // etiquetas por fuera
                serieTipos["PieDrawingStyle"] = "Concave"; // estilo 3D suave
                serieTipos["PieLineColor"] = "Black"; // línea de unión
                serieTipos.BorderColor = Color.White;
                serieTipos.BorderWidth = 1;

                using (var cmd2 = new SQLiteCommand(queryTipos, conn))
                using (var reader2 = cmd2.ExecuteReader())
                {
                    while (reader2.Read())
                    {
                        string tipo = reader2["TipoEvento"].ToString();
                        int total = Convert.ToInt32(reader2["Total"]);
                        serieTipos.Points.AddXY(tipo, total);
                    }
                }

                chart2.Series.Add(serieTipos);

                // Expandir para ocupar todo el espacio disponible
                chart2.ChartAreas[0].Position = new ElementPosition(0, 0, 100, 100); // 100% del espacio
                chart2.Legends[0].Docking = Docking.Right;
                chart2.Legends[0].Alignment = StringAlignment.Far;
                chart2.Legends[0].Font = new Font("Arial", 9, FontStyle.Regular);
            }
        }

        /*private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void chart2_Click(object sender, EventArgs e)
        {

        }*/
    }
}
