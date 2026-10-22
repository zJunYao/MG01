using System;

public class GameHotFix : GameHotFixBase<GameHotFix>
{
	private MG01.Demo.DemoConfigurationSystem mDemoConfiguration;
	//----------------------------------------------------------------------------------------------------------------------------------
	protected override void registerAllTable()
	{
		mDemoConfiguration.RegisterTables();
	}
	protected override void registerAll()
	{
		LayoutRegisterHotFix.registeAll();
		MG01.Demo.DemoModule.Register();
    }
	protected override void initFrameSystem()
	{
		registeFrameSystem<MG01.Demo.DemoConfigurationSystem>(system => mDemoConfiguration = system);
		//registeFrameSystem<NetManager>((com) =>		{ mNetManager = com; });
	}
	protected override Type getStartGameSceneType() { return typeof(MainScene); }
}
