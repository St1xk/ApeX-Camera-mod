using TMPro;
using UnityEngine.UI;

namespace StickCameraMod.Core.Interface.Panel_Handlers;

public class TimeHandler : PanelHandlerBase
{
    protected override void Start()
    {
        transform.GetChild(2).GetComponent<Button>().onClick.AddListener(() =>
                                                                         {
                                                                             for (int i = 0;
                                                                                  i < BetterDayNightManager.instance
                                                                                         .weatherCycle.Length;
                                                                                  i++)
                                                                                 BetterDayNightManager.instance
                                                                                                .weatherCycle[i] =
                                                                                         BetterDayNightManager
                                                                                                .WeatherType
                                                                                                .Raining;
                                                                         });

        transform.GetChild(3).GetComponent<Button>().onClick.AddListener(() =>
                                                                         {
                                                                             for (int i = 0;
                                                                                  i < BetterDayNightManager.instance
                                                                                         .weatherCycle.Length;
                                                                                  i++)
                                                                                 BetterDayNightManager.instance
                                                                                                .weatherCycle[i] =
                                                                                         BetterDayNightManager
                                                                                                .WeatherType
                                                                                                .None;
                                                                         });

        try
        {
            transform.GetChild(1).GetComponentInChildren<Slider>().maxValue =
                    BetterDayNightManager.instance.timeOfDayRange.Length;
        }
        catch
        {
            // ignored
        }

        transform.GetChild(1).GetComponentInChildren<Slider>().onValueChanged.AddListener(value =>
        {
            int valueReal = (int)value;
            BetterDayNightManager.instance.SetTimeOfDay(valueReal);
            transform.GetChild(1).GetComponentInChildren<TextMeshProUGUI>().text = $"Current Time: {valueReal}";
        });

        base.Start();
    }

    private void OnEnable()
    {
        try
        {
            transform.GetChild(1).GetComponentInChildren<Slider>().maxValue =
                    BetterDayNightManager.instance.timeOfDayRange.Length;
        }
        catch
        {
            // ignored
        }
    }
}