using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AtBatController
{
    // ------------------------------------------------------------
    // 打者の入れ替え
    // ------------------------------------------------------------
    private PlayerStats OpponentBatter(int i)
    {
        PlayerStats p;
        switch (i % 3)
        {
            case 0:
                p = new PlayerStats { Name = "ライバルA", Position = 5, Species = "human", BatsRight = true,
                    Meet = 50, Power = 44, Speed = 45, Field = 40, Arm = 40 };
                break;
            case 1:
                p = new PlayerStats { Name = "ライバルB", Position = 4, Species = "human", BatsRight = false,
                    Meet = 60, Power = 36, Speed = 56, Field = 44, Arm = 40 };
                break;
            default:
                p = new PlayerStats { Name = "ライバルC", Position = 3, Species = "human", BatsRight = true,
                    Meet = 44, Power = 60, Speed = 36, Field = 36, Arm = 40 };
                break;
        }
        SpeciesTable.Apply(p);
        return p;
    }

    private void SetupBatter()
    {
        // 打席ごとに画像キャッシュの上限を確認(超えていなければ何もしない)
        if (CharaCatalog.OverBudget) CharaCatalog.TrimArt(false);
        Color uniform, accent;
        if (_userTeam == null) BuildTeams();
        if (_gs.UserBatting)
        {
            _batterIndex = (_batterIndex + 1) % _userTeam.Lineup.Count;
            _batterStats = CharaRoster.Stats(_userTeam.Lineup[_batterIndex]);
            uniform = BattingConfig.UserJersey; accent = BattingConfig.UserAccent;
        }
        else
        {
            _oppIndex = (_oppIndex + 1) % _oppTeam.Lineup.Count;
            _batterStats = CharaRoster.Stats(_oppTeam.Lineup[_oppIndex]);
            uniform = BattingConfig.OppJersey; accent = BattingConfig.OppAccent;
        }
        _batSide = _batterStats.BatsRight ? -1 : 1;

        if (_batter != null) Destroy(_batter.gameObject);
        _batter = RagdollPlayer.Create(transform, "Batter",
            new Vector2(_batSide * BattingConfig.BatterX, 0f), -_batSide,
            _batterStats, uniform, accent, true, _gs.UserBatting);
        _stage.AddShadow(_batter.Pelvis.transform, 1.6f, uniform);
        ApplyStackLook(_batter, _batterStats, _gs.UserBatting);
        _stage.Stadium.SetBoardBatter(_batterStats.Name);

        _cursor.Configure(HitJudge.CursorHalfW(_batterStats.Meet), HitJudge.CursorHalfH(_batterStats.Meet), _batSide);
        _cursor.SetVisible(_gs.UserBatting);
        _cursorPos = _cursorTarget = new Vector2(0f, BattingConfig.ZoneCenterY);
        if (_gs.UserBatting) _ui.SetBatter(_batterStats);
        else _ui.SetBatterLabel("相手 " + _batterStats.Name + "  ミート " + _batterStats.Meet + "  パワー " + _batterStats.Power);
    }

}
