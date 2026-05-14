# 구현 계획 - 대기실(Waiting Room) 시스템 추가

## 개요
기존의 바로 게임 시작 방식 대신, 플레이어가 방에 입장한 후 대기실 UI에서 준비(Ready)를 하고 호스트가 게임을 시작(Start)하는 흐름으로 변경합니다.

## 변경 사항

### 1. `LobbyManager.cs` 수정
- `NetworkBehaviour`를 상속받도록 클래스 정의 변경 (네트워크 변수 및 RPC 사용을 위해).
- **UI 패널 관리**:
  - `lobbyPanel`: 기존 IP 입력 및 방 생성/입장 화면.
  - `roomPanel`: 대기실 화면 (새로 추가).
- **UI 요소 추가 (RoomPanel)**:
  - `player1StatusText`: 호스트 상태 표시.
  - `player2StatusText`: 게스트 상태 표시.
  - `readyButton`: 게스트용 레디 버튼.
  - `startButton`: 호스트용 게임 시작 버튼.
  - `leaveButton`: 방 나가기 버튼.
- **네트워크 로직**:
  - `NetworkVariable<bool> isGuestReady`: 게스트의 레디 상태 동기화.
  - `OnClientConnectedCallback`, `OnClientDisconnectCallback`: 플레이어 입장/퇴장 감지하여 UI 갱신.
  - `ReadyServerRpc`: 게스트가 레디 버튼 누르면 호출.
  - `StartGameServerRpc`: 호스트가 시작 버튼 누르면 호출 (게스트 레디 체크).

### 2. UI 워크플로우
1. 사용자가 방 만들기/들어가기 버튼 클릭.
2. 접속 성공 시 `lobbyPanel` 비활성화, `roomPanel` 활성화.
3. 대기실에서 상태 갱신 (Player joined, Ready status).
4. 호스트가 Start Game 클릭 시 `GameScene`으로 씬 전환.

## 작업 순서
1. `LobbyManager.cs` 코드 전면 수정 (패널 교체 및 대기실 로직 추가).
2. 유니티 에디터 설정 가이드(`walkthrough.md`) 업데이트 (새로운 UI 패널 및 컴포넌트 연결).
