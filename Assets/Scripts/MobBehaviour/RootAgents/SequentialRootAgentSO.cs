using System.Collections.Generic;
using System.Linq;
using Sirenix.Serialization;
using UnityEngine;

namespace miniRAID.TurnSchedule.RootAgent
{
    public class SequentialRootAgentSO : MobRootAgentBaseSO
    {
        public MobRootAgentBaseSO[] agents;

        public override MobListener Wrap(MobData parent)
        {
            return new SequentialRootAgent(parent, this);
        }

        public override void ModifyTurnSlicesInPlace(Timestamp now, MobRootAgentBase agent, TurnScheduleSequence schedule)
        {
            throw new System.NotImplementedException();
        }
    }

    public class SequentialRootAgent : MobRootAgentBase
    {
        [SerializeField] private MobRootAgentBase[] rootAgents;
        private SequentialRootAgentSO seqData => (SequentialRootAgentSO)data;

        public SequentialRootAgent(MobData parent, MobRootAgentBaseSO data) : base(parent, data)
        {
            rootAgents = seqData.agents.Select(x => (MobRootAgentBase)(x.Wrap(parent))).ToArray();
        }

        public override void ModifyTurnSlicesInPlace(Timestamp now, TurnScheduleSequence schedule)
        {
            foreach (var agent in rootAgents)
            {
                agent.ModifyTurnSlicesInPlace(now, schedule);
            }
        }
    }
}
