using System;
using System.Collections;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace miniRAID.TurnSchedule
{
    public abstract class AbstractTurnSliceSO : CustomIconScriptableObject
    {
        public Sprite barIcon;
        public Color mainColor;
        public string label;
        public UnityEngine.Localization.LocalizedString labelKey;
        public bool showInUI = true;

        public TurnSliceCategory defaultCategory;

        [Title("Sheduling Filters", "Leave empty for no filters")] 
        public int[] allowedTurns;
        public string[] allowedPhases;

        public abstract IEnumerator Turn(TurnSlice slice, CombatSchedulerCoroutine coroutine);

        public TurnSlice DummyWrap(TurnSliceMetadata metadata)
        {
            if (!ScheduleFilter(metadata)) return null;
            return new DummyTurnSlice(this, metadata);
        }

        public virtual bool ScheduleFilter(TurnSliceMetadata meta)
        {
            bool turnOK, phaseOK;
            
            if (allowedTurns == null || allowedTurns.Length == 0)
            {
                turnOK = true;
            }
            else
            {
                turnOK = allowedTurns.Contains(meta.timestamp.currentTurnID);
            }

            if (allowedPhases == null || allowedPhases.Length == 0)
            {
                phaseOK = true;
            }
            else
            {
                phaseOK = allowedPhases.Contains(meta.timestamp.currentPhase);
            }
            
            return turnOK && phaseOK;
        }
    }

    public abstract class TurnSliceSO : AbstractTurnSliceSO
    {
        public virtual TurnSlice Wrap(TurnSliceMetadata metadata)
        {
            if (!ScheduleFilter(metadata)) return null;
            return new TurnSlice(this, metadata);
        }
    }

    public enum TurnSliceCategory
    {
        Inherited = 0, // Default value
        PlayerTurn,
        EnemyRegularTurn,
        EnemySpecialTurn,
        AllyRegularTurn,
        AllySpecialTurn,
        UtilityTurn,
        Uncategorized,
    }
    
    public struct TurnSliceMetadata
    {
        public TurnSliceCategory category;
        public Timestamp timestamp;
        
        // TODO: We should really move this to somewhere else
        public ValueGetter<None, int> Priority;
        
        // TODO: FIXME: Savedata? How to handle this? Is this okay?
        public MobData source;

        public TurnSliceMetadata(MobData source)
        {
            this.category = TurnSliceCategory.Inherited;
            this.Priority = 0;

            this.source = source;
            this.timestamp = new Timestamp();
        }

        public TurnSliceMetadata(MobData source, int priority)
        {
            this.category = TurnSliceCategory.Inherited;

            this.Priority = priority;
            this.source = source;
            this.timestamp = new Timestamp();
        }
        
        public TurnSliceMetadata(MobData source, int priority, TurnSliceCategory category)
        {
            this.category = category;
            this.Priority = priority;
            this.source = source;
            this.timestamp = new Timestamp();
        }
    }

    public class TurnSlice : Backend.BackendState
    {
        public AbstractTurnSliceSO data;
        public TurnSliceMetadata metadata;

        public virtual bool ShowInUI => data.showInUI && (!muted);
        UnityEngine.Localization.Locale labelLocale;
        string localizedLabel;
        public virtual string Label
        {
            get
            {
                if(data.labelKey==null || data.labelKey.IsEmpty)return data.label;
                var locale=UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                if(localizedLabel==null || labelLocale!=locale)
                {labelLocale=locale;localizedLabel=Globals.localizer.L(data.labelKey)??data.label;}
                return localizedLabel;
            }
        }
        public virtual Color MainColor => data.mainColor;
        public virtual Sprite BarIcon => data.barIcon;
        
        protected CombatSchedulerCoroutine coroutine;

        // Set to true to effectively terminate this turn slice.
        public bool muted = false;

        public TurnSlice(AbstractTurnSliceSO data, TurnSliceMetadata metadata)
        {
            this.data = data;
            this.metadata = metadata;

            if (this.metadata.category == TurnSliceCategory.Inherited)
            {
                this.metadata.category = data.defaultCategory;
            }
        }
        
        public void RegisterTo(CombatSchedulerCoroutine coroutine)
        {
            this.coroutine = coroutine;
            Register();
        }

        public void Mute()
        {
            muted = true;
        }

        public virtual void OnRemove(CombatSchedulerCoroutine coroutine) { }

        public virtual IEnumerator Turn()
        {
            if (muted)
            {
                yield break;
            }

            yield return new JumpIn(data.Turn(this, coroutine));
        }
    }

    public class DummyTurnSlice : TurnSlice
    {
        public DummyTurnSlice(AbstractTurnSliceSO data, TurnSliceMetadata metadata) : base(data, metadata)
        {
            Mute();
        }

        public override bool ShowInUI => false;
    }
}