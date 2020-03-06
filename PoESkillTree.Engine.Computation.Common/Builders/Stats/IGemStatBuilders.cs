using PoESkillTree.Engine.Computation.Common.Builders.Skills;
using PoESkillTree.Engine.GameModel.Skills;

namespace PoESkillTree.Engine.Computation.Common.Builders.Stats
{
    /// <summary>
    /// Factory interface for stats related to gems.
    /// </summary>
    public interface IGemStatBuilders
    {
        IStatBuilder AdditionalActiveLevels(IGemTagBuilder gemTag);
        IStatBuilder AdditionalActiveSpellLevels(IGemTagBuilder gemTag);

        IStatBuilder AdditionalLevelsForModifierSourceItemSlot();
        IStatBuilder AdditionalLevelsForModifierSourceItemSlot(IGemTagBuilder gemTag);
        IStatBuilder AdditionalActiveLevelsForModifierSourceItemSlot();

        IStatBuilder AdditionalLevels(Skill skill);
    }
}