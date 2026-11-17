using System;
using AC_RoomDarkener.Core;
using UnityEngine;

namespace AC_RoomDarkener;

/// <summary>毎フレームの軽いポーリング (ホットキー + 状態遷移) と、終了時の強制復元を担当する常駐コンポーネント。</summary>
public sealed class RoomDarkenerBehaviour : MonoBehaviour
{
    // IL2CPP の MonoBehaviour に必須のコンストラクタ
    public RoomDarkenerBehaviour(IntPtr ptr) : base(ptr)
    {
    }

    private void Update()
    {
        RoomDarkness.HandleHotkey();
        RoomDarkness.Tick();
    }

    private void OnDestroy()
    {
        RoomDarkness.Shutdown();
    }

    private void OnApplicationQuit()
    {
        RoomDarkness.Shutdown();
    }
}
