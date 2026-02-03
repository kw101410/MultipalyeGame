using Unity.Netcode.Components;
using UnityEngine;

// 서버 권한 끄고 내 맘대로 움직이게 하는 스크립트
public class ClientNetworkTransform : NetworkTransform
{
    // 권한 확인: "이거 서버가 조종함?" -> "아니오(false)" 라고 답함
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}