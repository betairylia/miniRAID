using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using miniRAID.TurnSchedule.RootAgent;
using UnityEngine.UIElements;

namespace miniRAID.UIElements
{
    public class BossStatsController
    {
        VisualElement HPBar;
        Label nameLevelLabel, buffList, incomingList, debuffList, hpNumber, hpPercentage;

        VisualElement masterElem;

        MobData boss;

        MobRootAgentBase agent;

        public BossStatsController(VisualElement e)
        {
            masterElem = e;

            HPBar = e.Q<VisualElement>("HPContent");

            nameLevelLabel = e.Q<Label>("Name");
            buffList = e.Q<Label>("Buffs");
            incomingList = e.Q<Label>("Incoming");
            debuffList = e.Q<Label>("Debuffs");
            hpNumber = e.Q<Label>("HPNum");
            hpPercentage = e.Q<Label>("HPPercentage");
        }

        public void Register(MobRenderer mobRenderer)
        {
            this.boss = mobRenderer.data;
            this.agent = mobRenderer.data.FindListener<MobRootAgentBase>();
            Update();
        }

        public void Update()
        {
            if(boss == null)
            {
                nameLevelLabel.text = "UNKNOWN";
            }
            else
            {
                nameLevelLabel.text = $"Lv.{boss.level} {boss.nickname}";
                hpNumber.text = $"{boss.health} / {boss.maxHealth}";
                hpPercentage.text = $"{boss.health / (float)boss.maxHealth * 100.0f:0.0}%";

                HPBar.style.width = new StyleLength(new Length((float)boss.health / (float)boss.maxHealth * 100.0f, LengthUnit.Percent));

                string effects = "";
                foreach (var fx in boss.listeners)
                {
                    if (fx.type == MobListenerSO.ListenerType.Buff)
                    {
                        if(effects != "")
                        {
                            effects += " " + fx.name;
                        }
                        else
                        {
                            effects += fx.name;
                        }
                    }
                }

                buffList.text = $"BUFF LIST\n{effects}";
                debuffList.text = $"DEBUFF LIST\nNOT IMPLEMENTED";

                if (this.agent == null)
                {
                    incomingList.text = $"- INCOMING -\nNO AGENT";
                }
                else
                {
                    incomingList.text = $"- INCOMING -\n{agent.GetIncomingString(boss)}";
                }
            }
        }
    }
}
