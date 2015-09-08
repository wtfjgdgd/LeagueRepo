﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace OneKeyToWin_AIO_Sebby.Champions
{
    class Morgana
    {
        private Menu Config = Program.Config;
        public static Orbwalking.Orbwalker Orbwalker = Program.Orbwalker;

        private Spell E, Q, R, W;

        private float QMANA, WMANA, EMANA, RMANA;

        public Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        public void LoadOKTW()
        {
            Q = new Spell(SpellSlot.Q, 1300);
            W = new Spell(SpellSlot.W, 1000);
            E = new Spell(SpellSlot.E, 750);
            R = new Spell(SpellSlot.R, 600);

            Q.SetSkillshot(0.25f, 75f, 1200f, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.50f, 200f, 2200f, false, SkillshotType.SkillshotCircle);

            Config.SubMenu(Player.ChampionName).SubMenu("Q option").AddItem(new MenuItem("ts", "Use common TargetSelector").SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Q option").AddItem(new MenuItem("ts1", "ON - only one target"));
            Config.SubMenu(Player.ChampionName).SubMenu("Q option").AddItem(new MenuItem("ts2", "OFF - all targets"));

            Config.SubMenu(Player.ChampionName).SubMenu("Q option").AddItem(new MenuItem("qCC", "Auto Q cc & dash enemy").SetValue(true));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu(Player.ChampionName).SubMenu("Q option").SubMenu("Use on").AddItem(new MenuItem("grab" + enemy.ChampionName, enemy.ChampionName).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("W option").AddItem(new MenuItem("autoW", "Auto W").SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("W option").AddItem(new MenuItem("autoWcc", "Auto W only CC enemy").SetValue(false));

            Config.SubMenu(Player.ChampionName).SubMenu("R option").AddItem(new MenuItem("rCount", "Auto R if enemies in range").SetValue(new Slider(3, 0, 5)));
            Config.SubMenu(Player.ChampionName).SubMenu("R option").AddItem(new MenuItem("rKs", "R ks").SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("R option").AddItem(new MenuItem("inter", "OnPossibleToInterrupt")).SetValue(true);
            Config.SubMenu(Player.ChampionName).SubMenu("R option").AddItem(new MenuItem("Gap", "OnEnemyGapcloser")).SetValue(true);    

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (enemy.Spellbook.Spells[i] != null)
                    {
                        var spell = enemy.Spellbook.Spells[i];
                        Config.SubMenu(Player.ChampionName).SubMenu("E Shield Config").SubMenu("Spell Manager").SubMenu(enemy.ChampionName).AddItem(new MenuItem("spell" + spell.SData.Name, spell.Name).SetValue(true));
                    }
                }
            }

            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team == Player.Team))
            {
                Config.SubMenu(Player.ChampionName).SubMenu("E Shield Config").SubMenu("Shield ally").SubMenu(ally.ChampionName).AddItem(new MenuItem("skillshot" + ally.ChampionName, "skillshot").SetValue(true));
                Config.SubMenu(Player.ChampionName).SubMenu("E Shield Config").SubMenu("Shield ally").SubMenu(ally.ChampionName).AddItem(new MenuItem("targeted" + ally.ChampionName, "targeted").SetValue(true));
                Config.SubMenu(Player.ChampionName).SubMenu("E Shield Config").SubMenu("Shield ally").SubMenu(ally.ChampionName).AddItem(new MenuItem("HardCC" + ally.ChampionName, "Hard CC").SetValue(true));
                Config.SubMenu(Player.ChampionName).SubMenu("E Shield Config").SubMenu("Shield ally").SubMenu(ally.ChampionName).AddItem(new MenuItem("Poison" + ally.ChampionName, "Poison").SetValue(true));
            }

            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));

            Game.OnUpdate += Game_OnGameUpdate;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            
        }

        private void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (R.IsReady() && Config.Item("inter").GetValue<bool>() && sender.IsValidTarget(R.Range))
                R.Cast();
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {

            if (!E.IsReady() || args.SData.IsAutoAttack() || !sender.IsEnemy || !sender.IsValid<Obj_AI_Hero>() || Player.Distance(sender.ServerPosition) > 2000)
                return;
            
            if (Config.Item("spell" + args.SData.Name) != null && !Config.Item("spell" + args.SData.Name).GetValue<bool>())
                return;

            foreach (var ally in Program.Allies.Where(ally => ally.IsValid  && Player.Distance(ally.ServerPosition) < E.Range))
            {
                double dmg = 0;

                if (Config.Item("targeted" + ally.ChampionName).GetValue<bool>() && args.Target != null && args.Target.NetworkId == ally.NetworkId)
                {
                    dmg = dmg + sender.GetSpellDamage(ally, args.SData.Name);
                }
                else if (Config.Item("skillshot" + ally.ChampionName).GetValue<bool>())
                {
                    var castArea = ally.Distance(args.End) * (args.End - ally.ServerPosition).Normalized() + ally.ServerPosition;
                    if (castArea.Distance(ally.ServerPosition) > ally.BoundingRadius / 2)
                        continue;
                    dmg = dmg + sender.GetSpellDamage(ally, args.SData.Name);
                }
                
                if ( dmg > 0)
                    E.CastOnUnit(ally);
            }   
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (R.IsReady() && Config.Item("Gap").GetValue<bool>() && gapcloser.Sender.IsValidTarget(R.Range))
                R.Cast();
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (Program.LagFree(1) && Q.IsReady())
                LogicQ();
            if (Program.LagFree(2) && R.IsReady())
                LogicR();
            if (Program.LagFree(3) && W.IsReady() && Config.Item("autoW").GetValue<bool>())
                LogicW();
            if (Program.LagFree(4) && E.IsReady())
                LogicE();
        }


        private void LogicE()
        {
            foreach (var ally in Program.Allies.Where(ally => ally.IsValid && ally.Distance(Player.Position) < E.Range))
            {
                if (Config.Item("HardCC" + ally.ChampionName).GetValue<bool>() && HardCC(ally))
                {
                    E.CastOnUnit(ally);
                }
                else if (Config.Item("Poison" + ally.ChampionName).GetValue<bool>() && ally.HasBuffOfType(BuffType.Poison))
                {
                    E.CastOnUnit(ally);
                }
            }
        }

        private void LogicQ()
        {
            if (Program.Combo && Config.Item("ts").GetValue<bool>())
            {
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);

                if (t.IsValidTarget(Q.Range) && !t.HasBuffOfType(BuffType.SpellImmunity) && !t.HasBuffOfType(BuffType.SpellShield) && Config.Item("grab" + t.ChampionName).GetValue<bool>())
                    Program.CastSpell(Q, t);
            }
            foreach (var t in Program.Enemies.Where(t => t.IsValidTarget(Q.Range) && Config.Item("grab" + t.ChampionName).GetValue<bool>()))
            {
                if (!t.HasBuffOfType(BuffType.SpellImmunity) && !t.HasBuffOfType(BuffType.SpellShield))
                {
                    if (Program.Combo && !Config.Item("ts").GetValue<bool>())
                        Program.CastSpell(Q, t);

                    if (Config.Item("qCC").GetValue<bool>())
                    {
                        if (!OktwCommon.CanMove(t))
                            Q.Cast(t, true);
                        Q.CastIfHitchanceEquals(t, HitChance.Dashing);
                        Q.CastIfHitchanceEquals(t, HitChance.Immobile);
                    }
                }
            }
        }

        private void LogicR()
        {
            bool rKs = Config.Item("rKs").GetValue<bool>();
            foreach (var target in Program.Enemies.Where(target => target.IsValidTarget(R.Range) && target.HasBuff("rocketgrab2")))
            {
                if (rKs && R.GetDamage(target) > target.Health)
                    R.Cast();
            }
            if (Player.CountEnemiesInRange(R.Range) >= Config.Item("rCount").GetValue<Slider>().Value && Config.Item("rCount").GetValue<Slider>().Value > 0)
                R.Cast();
        }
        private void LogicW()
        {
            var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget() )
            {
                if (!Config.Item("autoWcc").GetValue<bool>() && !Q.IsReady())
                {
                    var qDmg = Q.GetDamage(t);
                    var wDmg = W.GetDamage(t);
                    if (wDmg > t.Health)
                    {
                        Program.CastSpell(W, t);
                    }
                    else if (wDmg + qDmg > t.Health && Q.IsReady())
                        Program.CastSpell(W, t);
                    else if (Program.Combo && Player.Mana > RMANA + WMANA + EMANA + QMANA)
                        Program.CastSpell(W, t);
                }

                foreach (var enemy in Program.Enemies.Where(enemy => enemy.IsValidTarget(W.Range) && !OktwCommon.CanMove(enemy)))
                    W.Cast(enemy, true);
            }
        }

        private bool HardCC(Obj_AI_Hero target)
        {
            if (target.HasBuffOfType(BuffType.Stun) || target.HasBuffOfType(BuffType.Snare) || target.HasBuffOfType(BuffType.Knockup) ||
                target.HasBuffOfType(BuffType.Charm) || target.HasBuffOfType(BuffType.Fear) || target.HasBuffOfType(BuffType.Knockback) ||
                target.HasBuffOfType(BuffType.Taunt) || target.HasBuffOfType(BuffType.Suppression) ||
                target.IsStunned)
            {
                return true;

            }
            else
                return false;
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (Q.IsReady())
                        Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (E.IsReady())
                        Utility.DrawCircle(Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
            }
            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
        }
    }
}
