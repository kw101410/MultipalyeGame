# 워크스루: NetworkManager 오류 해결 (No Transport selected!)

## 개요
`[Netcode] No transport has been selected!` 에러는 `NetworkManager` 컴포넌트에 통신을 담당할 **Transport** 컴포넌트가 연결되지 않아서 발생합니다.

## 해결 단계

### 1. NetworkManager 찾기
1. **LobbyScene**의 Hierarchy 창에서 **NetworkManager** 오브젝트를 선택합니다.

### 2. Unity Transport 컴포넌트 확인
1. Inspector 창을 봅니다.
2. `Unity Transport`라는 컴포넌트가 붙어있는지 확인합니다.
   *   **만약 없다면**: 하단 `Add Component` 버튼 클릭 -> `Unity Transport` 검색 및 추가.

### 3. Network Transport 연결 (중요!)
1. **NetworkManager** 컴포넌트의 설정 항목 중 **Network Transport**라는 슬롯을 찾습니다. (보통 맨 위쪽이나 `Transport` 섹션에 있습니다)
2. 현재 이 슬롯이 `None (Network Transport)`으로 비어있을 것입니다.
3. 방금 확인한(또는 추가한) **Unity Transport** 컴포넌트(이름 부분을 잡고)를 드래그해서 **Network Transport** 슬롯에 넣어줍니다.
   *   또는 슬롯 옆의 동그라미(⊙) 버튼을 눌러 목록에서 선택합니다.

### 4. 기타 필수 설정
1. **Player Prefab**: `NetworkManager` 컴포넌트의 `Player Prefab` 슬롯에 **PlayerController** 프리팹이 할당되어 있는지 확인하세요. 없으면 게임 시작 시 캐릭터가 안 나옵니다.
2. **Network Prefabs**: 총알이나 무기 등 네트워크로 동기화되는 다른 프리팹들도 리스트에 등록되어 있어야 합니다.

이 설정을 완료하고 다시 실행해 보세요.
