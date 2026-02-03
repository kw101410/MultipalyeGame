using Unity.Netcode.Components;
using UnityEngine;

// 기본 NetworkAnimator를 상속받아서 권한만 바꿔치기함
public class OwnerNetworkAnimator : NetworkAnimator
{
    // 팩트: 이거 false로 리턴해야 "내 캐릭터 애니메이션은 내가 정한다"가 됨.
    // FPS처럼 반응속도 중요한 겜은 이거 필수임.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}