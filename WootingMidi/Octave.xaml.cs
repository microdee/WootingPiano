using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WootingMidi
{
    /// <summary>
    /// Interaction logic for Octave.xaml
    /// </summary>
    public partial class Octave : UserControl
    {
        public Octave()
        {
            InitializeComponent();
            _keyLabels = new[] { CKey, CSharpKey, DKey, DSharpKey, EKey, FKey, FSharpKey, GKey, GSharpKey, AKey, ASharpKey, HKey };
            Bars = new[] { C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, H };
            VelBars = new[] { CVel, CSharpVel, DVel, DSharpVel, EVel, FVel, FSharpVel, GVel, GSharpVel, AVel, ASharpVel, HVel };
        }

        private TextBlock[] _keyLabels;
        public ProgressBar[] Bars { get; }
        public ProgressBar[] VelBars { get; }

        public void FillKeys(string keys)
        {
            for (int i = 0; i < keys.Length && i < _keyLabels.Length; i++)
            {
                _keyLabels[i].Text = keys[i].ToString();
            }
        }

        public void UpdateBars(NoteKey[] bars, int offset)
        {
            for (int i = 0; i < Bars.Length; i++)
            {
                var ii = i + offset;
                if(ii > bars.Length - 1) break;
                if(bars[ii] == null) continue;
                Bars[i].Value = bars[ii].Pressure * 100;
                VelBars[i].Value = bars[ii].Velocity * 100;
            }
        }
    }
}
