using Autofac;
using AssettoServer.Server.Plugin;

namespace SonmezKomboPlugin;

public class SonmezKomboModule : AssettoServerModule<SonmezKomboConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(c => new ScoreStore(c.Resolve<SonmezKomboConfiguration>().ScoreFile))
            .AsSelf().SingleInstance();
        builder.Register(c => new HubState(c.Resolve<SonmezKomboConfiguration>().HubKey))
            .AsSelf().SingleInstance();
        builder.RegisterType<SonmezKombo>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
