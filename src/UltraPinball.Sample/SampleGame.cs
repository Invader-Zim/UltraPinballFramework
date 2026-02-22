using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using UltraPinball.Sample.Modes;

namespace UltraPinball.Sample;

public class SampleGame : GameController
{
    public SampleGame(SampleMachine machine, IHardwarePlatform platform, ILoggerFactory loggerFactory)
        : base(machine, platform, loggerFactory) { }

    protected override void OnStartup()
    {
        RegisterMode(new HighScoreMode(new JsonHighScoreRepository("high_scores.json")));

        RegisterMode(new AttractMode());

        var trough = new TroughMode(["Trough0", "Trough1", "Trough2", "Trough3", "Trough4"])
        {
            AutoBallSaveSeconds = 8f
        };
        RegisterMode(trough);

        var tilt = new TiltMode(flippers:
            [
                new FlipperConfig("LeftFlipper",  "LeftFlipperMain",  PulseMs: 30),
                new FlipperConfig("RightFlipper", "RightFlipperMain", PulseMs: 30),
            ]);
        tilt.Tilted += () => trough.StopBallSave();
        RegisterMode(tilt);

        RegisterMode(new BallSearchMode(
            searchCoilNames: ["LeftSlingCoil", "RightSlingCoil"]));

        var bonus = new BonusMode();
        trough.BallDrained += () =>
        {
            if (tilt.IsTilted)
                EndBall();           // skip bonus after tilt — ball already penalised
            else
                bonus.StartBonus();
        };
        RegisterMode(bonus);

        RegisterMode(new AutoLaunchMode());
        RegisterMode(new SingleBall(bonus));
    }
}
