using Microsoft.AspNetCore.Components;

namespace HPSystemsTools.Views.Components
{
    public partial class GaugeComponent : ComponentBase
    {
        [Parameter] public string Title { get; set; } = "";
        [Parameter] public int CurrentValue { get; set; }
        [Parameter] public int MaxValue { get; set; }
        [Parameter] public string UnitOfMeasure { get; set; } = "";

        string Transform
        {
            get
            {
                if (MaxValue == 0)
                    return $"rotate(0turn)";

                if (CurrentValue >= MaxValue)
                    return $"rotate(0.5turn)";

                if (CurrentValue == 0)
                    return $"rotate(0.01turn)";

                var perc = CurrentValue * 50 / MaxValue;
                string rotation = $"rotate(0.{perc.ToString("00")}turn)";
                return rotation; //rotation;
            }
        }

        string Background
        {
            get
            {
                if (MaxValue == 0)
                    return $"#FFFFFF";

                if (CurrentValue >= MaxValue)
                    return $"#FF0000";

                var perc = (100 * CurrentValue) / MaxValue;
                var red = (perc * 255) / 100;
                var green = 255 - red;
                string background = $"#{red.ToString("X2")}{green.ToString("X2")}00";
                return background; //background;
            }
        }

        string Text
        {
            get
            {
                if (MaxValue == 0)
                    return $"-";

                if (CurrentValue >= MaxValue)
                    return $"+";
                return $"{CurrentValue}{UnitOfMeasure}";
            }
        }
    }
}
