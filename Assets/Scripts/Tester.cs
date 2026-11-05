using UnityEngine;
using Core.ZLog;
using GamePlay.Player.Simulation;
using GamePlay.Weapon;
using Sirenix.OdinInspector;

/// <summary>演示 AK47 准备、连续射击和换弹的 Tick 驱动。</summary>
public class Tester : MonoBehaviour
{
    /// <summary>可选的 AK47 配置资源。</summary>
    [SerializeField] private AK47SO _weaponData;

    /// <summary>示例使用的独立模拟时钟。</summary>
    private readonly SimulationClock _clock = new();

    /// <summary>当前演示的枪械实例。</summary>
    private AK47 _gun;

    /// <summary>当前已经推进的模拟拍。</summary>
    private uint _tick;

    /// <summary>本次演示已经发射的数量。</summary>
    private int _shotsFired;

    /// <summary>是否已经开始演示换弹。</summary>
    private bool _reloadRequested;
    GameInput input;

    /// <summary>创建枪械并开始准备动作。</summary>
    private void Start()
    {
        _gun = _weaponData != null ? _weaponData.CreateGun() : new AK47();
        _gun.OnShot += OnShot;
        _gun.OnReloadCompleted += OnReloadCompleted;
        _clock.Reset(Time.timeAsDouble);
        _gun.Tick(_tick);
        _gun.TryPrepare();
        input=new();
        input.Enable();
    }

    /// <summary>按固定模拟步长演示三次射击和一次换弹。</summary>
    private void Update()
    {
        if (_gun == null)
            return;

        _clock.Advance(Time.timeAsDouble);
        int steps = 0;
        while (steps < SimTime.MaxStepsPerFrame && _clock.TryStep())
        {
            steps++;
            _gun.Tick(unchecked(++_tick));
        }
    }

    /// <summary>记录射击次数和当前弹量。</summary>
    private void OnShot()
    {
        _shotsFired++;
        ZLog.Log<Tester>($"AK47 射击 {_shotsFired}，弹匣 {_gun.Magazine.Rounds}，备弹 {_gun.Magazine.ReserveRounds}");
    }

    /// <summary>记录换弹完成后的弹量。</summary>
    private void OnReloadCompleted()
    {
        ZLog.Log<Tester>($"AK47 换弹完成，弹匣 {_gun.Magazine.Rounds}，备弹 {_gun.Magazine.ReserveRounds}");
    }

    /// <summary>释放当前示例使用的事件订阅。</summary>
    private void OnDestroy()
    {
        if (_gun == null)
            return;

        _gun.OnShot -= OnShot;
        _gun.OnReloadCompleted -= OnReloadCompleted;
    }

    [Button]
    public void TryFire()=>_gun.TryFire();
    [Button]
    public void TryReload()=>_gun.TryReload();
}
