
public class GameEntry : GameEntryBase
{
	public override void Awake()
	{
		base.Awake();
		Game framework = new();
		setFrameworkAOT(framework);
		framework.init();
	}
}
