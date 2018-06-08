﻿using System;
using System.Collections.Generic;
using System.Linq;
using PoESkillTree.Common.Utils;
using PoESkillTree.Computation.Builders.Values;
using PoESkillTree.Computation.Common;
using PoESkillTree.Computation.Common.Builders;
using PoESkillTree.Computation.Common.Builders.Conditions;
using PoESkillTree.Computation.Common.Builders.Entities;
using PoESkillTree.Computation.Common.Builders.Resolving;
using PoESkillTree.Computation.Common.Builders.Stats;
using PoESkillTree.Computation.Common.Builders.Values;

namespace PoESkillTree.Computation.Builders.Stats
{
    public class StatBuilder : IStatBuilder
    {
        private readonly string _identity;
        private readonly IEntityBuilder _entityBuilder;
        private readonly bool _isRegisteredExplicitly;
        private readonly Type _dataType;
        private readonly IReadOnlyCollection<Behavior> _behaviors;
        private readonly Func<IStat, IStat> _statConverter;

        public StatBuilder(
            string identity, IEntityBuilder entityBuilder, bool isRegisteredExplicitly = false, Type dataType = null,
            IReadOnlyCollection<Behavior> behaviors = null)
            : this(identity, entityBuilder, isRegisteredExplicitly, dataType, behaviors, null)
        {
        }

        private StatBuilder(
            string identity, IEntityBuilder entityBuilder, bool isRegisteredExplicitly, Type dataType,
            IReadOnlyCollection<Behavior> behaviors, Func<IStat, IStat> statConverter)
        {
            _identity = identity;
            _entityBuilder = entityBuilder;
            _isRegisteredExplicitly = isRegisteredExplicitly;
            _dataType = dataType;
            _behaviors = behaviors;
            _statConverter = statConverter ?? Funcs.Identity;
        }

        private StatBuilder With(IEntityBuilder entityBuilder) =>
            new StatBuilder(_identity, entityBuilder, _isRegisteredExplicitly, _dataType, _behaviors, _statConverter);

        private StatBuilder With(Func<IStat, IStat> statConverter) =>
            new StatBuilder(_identity, _entityBuilder, _isRegisteredExplicitly, _dataType, _behaviors, 
                s => statConverter(_statConverter(s)));

        public IStatBuilder Resolve(ResolveContext context) => With(_entityBuilder.Resolve(context));

        public IStatBuilder Minimum => With(s => s.Minimum);
        public IStatBuilder Maximum => With(s => s.Maximum);

        public ValueBuilder Value =>
            new ValueBuilder(new ValueBuilderImpl(BuildValue, c => Resolve(c).Value));

        private IValue BuildValue(Entity modifierSourceEntity)
        {
            var stats = BuildStats(modifierSourceEntity);
            if (stats.Count != 1)
                throw new InvalidOperationException(
                    "Can only access the value of IStatBuilders that represent a single stat");

            return new FunctionalValue(c => c.GetValue(stats.Single()), $"{_identity}.Value");
        }

        public IStatBuilder ConvertTo(IStatBuilder stat) => throw new NotImplementedException();
        public IStatBuilder GainAs(IStatBuilder stat) => throw new NotImplementedException();

        public IFlagStatBuilder ApplyModifiersTo(IStatBuilder stat, IValueBuilder percentOfTheirValue) =>
            throw new NotImplementedException();

        public IStatBuilder ChanceToDouble => throw new NotImplementedException();

        public IStatBuilder For(IEntityBuilder entity) => With(entity);

        public IStatBuilder WithCondition(IConditionBuilder condition) => throw new NotImplementedException();

        public IStatBuilder CombineWith(IStatBuilder other) => throw new NotImplementedException();

        public (IReadOnlyList<IStat> stats, ModifierSource modifierSource, ValueConverter valueConverter)
            Build(ModifierSource originalModifierSource, Entity modifierSourceEntity)
        {
            var stats = BuildStats(modifierSourceEntity);
            return (stats, originalModifierSource, Funcs.Identity);
        }

        private IReadOnlyList<IStat> BuildStats(Entity modifierSourceEntity)
        {
            var entities = _entityBuilder.Build(modifierSourceEntity).DefaultIfEmpty(modifierSourceEntity);
            return entities.Select(CreateStat).Select(_statConverter).ToList();
        }

        private IStat CreateStat(Entity modifierSourceEntity) =>
            new Stat(_identity, modifierSourceEntity, _isRegisteredExplicitly, _dataType, _behaviors);
    }
}