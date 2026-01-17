using Diplom;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Diplom.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ISeries[] Series { get; set; }
        = new ISeries[]
        {
            new PieSeries<double> { Values = new double[] { 2 } },
            new PieSeries<double> { Values = new double[] { 4 } }, 
            new PieSeries<double> { Values = new double[] { 1 } },
            new PieSeries<double> { Values = new double[] { 4 } },
            new PieSeries<double> { Values = new double[] { 3 } }
        };
}