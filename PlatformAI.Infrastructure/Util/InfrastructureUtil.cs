using AutoMapper;

namespace PlatformAI.Infrastructure;
public static class InfrastructureUtil
{
    public static Mapper MapperManager = new Mapper(MapperConfigurator.Configure);
}