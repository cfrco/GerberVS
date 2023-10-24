using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GerberVS;

namespace GerberTranslator
{
    public partial class Form1 : Form
    {
        private LibGerberVS libGerberVs = new LibGerberVS();
        private GerberProject gProject = new GerberProject();

        public Form1()
        {
            InitializeComponent();

            string fileName = "test.gdo";
            openLayer(fileName);

            Aperture[] apertures = gProject.FileInfo[0].Image.ApertureArray();
            for (int i = 0; i < apertures.Length; i++)
            {
                Aperture a = apertures[i];
                if (a == null)
                {
                    continue;
                }
                double[] parameters = new double[a.ParameterCount];
                Array.Copy(a.Parameters(), parameters, a.ParameterCount);
                writeLine($"Ape D{i} {a.ApertureType} {am(a.ApertureMacro)} | {string.Join(", ", parameters.Select(p => p.ToString()))}");
            }

            foreach (var g in gProject.FileInfo[0].Image.GerberNetList)
            {
                //writeLine($"Obj {g.Aperture} X={g.StartX} Y={g.StartY}");
            }
        }

        private string am(ApertureMacro apertureMacro)
        {
            if (apertureMacro == null)
            {
                return "";
            }
            return apertureMacro.Name;
        }

        private bool openLayer(string fileName)
        {
            try
            {
                libGerberVs.OpenLayerFromFileName(gProject, fileName);
                return true;
            }
            catch (GerberDllException ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += ex.InnerException.Message;
                }

                MessageBox.Show(errorMessage, "GerberView", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void writeLine(string line)
        {
            System.Diagnostics.Debug.WriteLine(line);
        }
    }
}
