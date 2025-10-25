namespace BattleV2.Targeting
{
    public static class ShapeResolverBootstrap
    {
        public static TargetResolverRegistry RegisterDefaults(TargetResolverRegistry registry)
        {
            if (registry == null)
            {
                registry = new TargetResolverRegistry();
            }

            registry.Register(TargetShape.Single, new SingleTargetResolver());
            registry.Register(TargetShape.All, new GroupTargetResolver());
            return registry;
        }
    }
}
