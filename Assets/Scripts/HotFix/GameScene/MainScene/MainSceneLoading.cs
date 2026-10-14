using static LT;

public class MainSceneLoading : SceneProcedure
{
	protected override void onInit(SceneProcedure lastProcedure)
	{
		LOAD<UIMain>();
	}
	protected override void onExit(SceneProcedure nextProcedure) { }
}
