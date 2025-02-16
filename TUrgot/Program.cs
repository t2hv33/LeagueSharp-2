﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

#endregion

namespace TUrgot
{
    internal class Program
    {
        public const string ChampName = "Urgot";
        public static Orbwalking.Orbwalker Orbwalker;
        public static Obj_AI_Hero Player;

        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q, Q2, W, E;
        public static SpellDataInst Ignite;
        public static Menu Menu;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.ChampionName != ChampName)
            {
                return;
            }

            Q = new Spell(SpellSlot.Q, 1000);
            Q2 = new Spell(SpellSlot.Q, 1200);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 900);

            Q.SetSkillshot(0.10f, 100f, 1600f, true, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(0.10f, 100f, 1600f, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.283f, 0f, 1750f, false, SkillshotType.SkillshotCircle);

            SpellList.Add(Q);
            SpellList.Add(Q2);
            SpellList.Add(W);
            SpellList.Add(E);

            Player = ObjectManager.Player;
            Ignite = Player.Spellbook.GetSpell(Player.GetSpellSlot("summonerdot"));

            Menu = new Menu("Trees " + ChampName, ChampName, true);

            Menu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Orbwalker = new Orbwalking.Orbwalker(Menu.SubMenu("Orbwalker"));

            var ts = new Menu("Target Selector", "Target Selector");
            SimpleTs.AddToMenu(ts);
            Menu.AddSubMenu(ts);

            Menu.AddSubMenu(new Menu("Combo", "Combo"));
            Menu.SubMenu("Combo").AddItem(new MenuItem("ComboQ", "Use Q").SetValue(true));
            Menu.SubMenu("Combo").AddItem(new MenuItem("ComboE", "Use E").SetValue(true));
            Menu.SubMenu("Combo").AddItem(new MenuItem("ComboEChance", "E HitChance").SetValue(new Slider(1, 1, 3)));
            Menu.SubMenu("Combo")
                .AddItem(new MenuItem("ComboActive", "Combo").SetValue(new KeyBind(32, KeyBindType.Press)));

            Menu.AddSubMenu(new Menu("Harass", "Harass"));
            Menu.SubMenu("Harass").AddItem(new MenuItem("HarassQ", "Use Q").SetValue(true));
            Menu.SubMenu("Harass").AddItem(new MenuItem("HarassE", "Use E").SetValue(true));
            Menu.SubMenu("Harass").AddItem(new MenuItem("HarassEChance", "E HitChance").SetValue(new Slider(2, 1, 3)));
            Menu.SubMenu("Harass")
                .AddItem(new MenuItem("HarassActive", "Harass").SetValue(new KeyBind((byte) 'C', KeyBindType.Press)));

            Menu.AddSubMenu(new Menu("LaneClear", "LaneClear"));
            Menu.SubMenu("LaneClear").AddItem(new MenuItem("LaneClearQ", "Use Q").SetValue(true));
            Menu.SubMenu("LaneClear")
                .AddItem(
                    new MenuItem("LaneClearQManaPercent", "Minimum Q Mana Percent").SetValue(new Slider(30, 0, 100)));
            Menu.SubMenu("LaneClear")
                .AddItem(
                    new MenuItem("LaneClearActive", "LaneClear").SetValue(new KeyBind((byte) 'V', KeyBindType.Press)));

            Menu.AddSubMenu(new Menu("Drawings", "Drawings"));
            Menu.SubMenu("Drawings")
                .AddItem(new MenuItem("QRange", "Q").SetValue(new Circle(false, Color.Red, Q.Range)));
            Menu.SubMenu("Drawings")
                .AddItem(new MenuItem("ERange", "E").SetValue(new Circle(false, Color.Blue, E.Range)));

            //Menu.SubMenu("Drawings")
            //    .AddItem(new MenuItem("QTarget", "Draw Smart Q Target").SetValue(true));
            //Menu.SubMenu("Drawings")
            //    .AddItem(new MenuItem("BubbleThickness", "Bubble Thickness").SetValue(new Slider(15, 10, 25)));

            Menu.AddItem((new MenuItem("AutoQ", "Smart Q").SetValue(true)));
            Menu.AddToMainMenu();

            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnGameUpdate += Game_OnGameUpdate;

            Game.PrintChat("Trees" + ChampName + " loaded!");
            /*    try
            {


                var bubble = new Bubble(Player, Color.Red, 200, Menu.Item("BubbleThickness").GetValue<Slider>().Value);
            }
            catch (Exception e)
            {
                Game.PrintChat(e.ToString());
            }
            */
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            if (Menu.Item("LaneClearActive").GetValue<KeyBind>().Active)
            {
                LaneClear();
                return;
            }

            CastLogic();
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            Circle[] Draw = { Menu.Item("QRange").GetValue<Circle>(), Menu.Item("ERange").GetValue<Circle>() };

            foreach (var circle in Draw.Where(circle => circle.Active))
            {
                Utility.DrawCircle(Player.Position, circle.Radius, circle.Color);
            }


            if (!Menu.Item("QTarget").GetValue<bool>())
            {
                return;
            }

            foreach (var hero in
                ObjectManager.Get<Obj_AI_Hero>()
                    .Where(
                        hero => hero.IsValid && hero.IsVisible && !hero.IsDead && hero.HasBuff("UrgotPlasmaGrenadeBoom"))
                )
            {
                BubbleMark(hero.Position, Color.Red, 200, Menu.Item("BubbleThickness").GetValue<Slider>().Value);
            }
        }


        private static void BubbleMark(Vector3 position, Color color, float radius = 125, float thickness = 25)
        {
            var rSquared = radius * radius;

            for (var i = 1; i < thickness; i++)
            {
                var ycircle = (i * (radius / thickness * 2) - radius);
                var r = Math.Sqrt(rSquared - ycircle * ycircle);
                ycircle /= 1.3f;

                Drawing.DrawCircle(new Vector3(position.X, position.Y, position.Z + 100 + ycircle), (float) r, color);
            }
        }

        private static void LaneClear()
        {
            if (!Q.IsReady() || !Player.CanCast)
            {
                return;
            }
            var unit =
                ObjectManager.Get<Obj_AI_Minion>()
                    .First(
                        minion =>
                            minion.IsValid && minion.IsVisible && !minion.IsDead &&
                            minion.IsValidTarget(Q.Range, true, Player.ServerPosition) &&
                            DamageLib.IsKillable(minion, new[] { DamageLib.SpellType.Q }));

            CastQ(unit, "LaneClear");
        }

        private static void CastLogic()
        {
            KSLogic();
            SmartQ();

            var target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Physical);
            if (target == null ||
                (!Menu.Item("ComboActive").GetValue<KeyBind>().Active &&
                 !Menu.Item("HarassActive").GetValue<KeyBind>().Active))
            {
                return;
            }

            var mode = Menu.Item("ComboActive").GetValue<KeyBind>().Active ? "Combo" : "Harass";

            CastE(SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical), mode);
            CastQ(target, mode);
        }

        private static void SmartQ()
        {
            if (!Q.IsReady() || !Menu.Item("AutoQ").GetValue<bool>())
            {
                return;
            }

            foreach (var obj in
                ObjectManager.Get<GameObject>()
                    .Where(
                        obj =>
                            obj is Obj_AI_Hero && obj.IsValid && obj.IsEnemy &&
                            ((Obj_AI_Hero) obj).HasBuff("UrgotPlasmaGrenadeBoom") &&
                            ((Obj_AI_Hero) obj).IsValidTarget(Q2.Range, true, Player.ServerPosition)))
            {
                W.Cast();
                Q2.Cast(obj.Position);
                //Game.PrintChat("Cast Q2");
            }
        }

        private static void CastQ(Obj_AI_Base target, string mode)
        {
            if (Q.IsReady() && Menu.Item(mode + "Q").GetValue<bool>() && Player.Distance(target) < Q.Range)
            {
                Q.Cast(target);
                //Game.PrintChat("Cast Q");
            }
        }

        private static void CastE(Obj_AI_Base target, string mode)
        {
            if (!E.IsReady() || !Menu.Item(mode + "E").GetValue<bool>())
            {
                return;
            }

            var hitchance = GetHitchance(Menu.Item(mode + "EChance").GetValue<Slider>().Value);

            //Game.PrintChat("Cast E");

            if (Player.ServerPosition.Distance(target.ServerPosition) < E.Range)
            {
                E.CastIfHitchanceEquals(target, hitchance);
            }
            else
            {
                E.CastIfHitchanceEquals(SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical), HitChance.High);
            }
        }

        private static void KSLogic()
        {
            if (Ignite != null && Ignite.Slot != SpellSlot.Unknown && Ignite.State == SpellState.Ready && Player.CanCast)
            {
                KSIgnite();
            }
        }


        private static void KSIgnite()
        {
            var dmg = 50 + 20 * Player.Level;
            var unit =
                ObjectManager.Get<Obj_AI_Hero>()
                    .First(
                        obj =>
                            obj.IsValid && obj.IsEnemy && obj.IsValidTarget(600, true, Player.ServerPosition) &&
                            obj.Health < dmg);
            
                Player.SummonerSpellbook.CastSpell(Ignite.Slot, unit);
            
        }


        private static HitChance GetHitchance(int num)
        {
            switch (num)
            {
                case 1:
                    return HitChance.Low;
                case 2:
                    return HitChance.Medium;
                case 3:
                    return HitChance.High;
                default:
                    return HitChance.High;
            }
        }
    }
}