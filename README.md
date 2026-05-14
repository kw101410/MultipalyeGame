# MultipalyeGame (FPS)

유니티를 기반으로 한 멀티플레이 FPS 게임 프로젝트입니다.

## 다른 컴퓨터에서 시작하기

이 프로젝트를 다른 컴퓨터에서 내려받아 작업할 때 다음 순서를 권장합니다.

### 1. 필수 도구 설치
* **Unity Hub & Editor**: 프로젝트 버전에 맞는 유니티 에디터를 설치하세요.
* **Git LFS**: 대용량 에셋(모델, 텍스처, 사운드)을 올바르게 다운로드하려면 Git LFS 설치가 필수입니다.
  * [git-lfs.com](https://git-lfs.com/)에서 다운로드 후 설치
  * 터미널에서 `git lfs install` 실행

### 2. 프로젝트 클론 (Clone)
```bash
git clone [리포지토리 주소]
cd MultipalyeGame
git lfs pull
```

### 3. 프로젝트 열기
* Unity Hub에서 `Add` 버튼을 눌러 `MultipalyeGame` 폴더를 선택합니다.
* 처음 열 때 `Library` 폴더를 생성하므로 시간이 다소 걸릴 수 있습니다.

## 프로젝트 구조 (Assets)

* **_Project**: (권장) 본인의 작업물들을 모아두는 곳
* **Animation**: 캐릭터 및 오브젝트 애니메이션
* **Fbx**: 3D 모델 파일
* **Prefabs**: 게임 오브젝트 프리팹
* **Scenes**: 게임 레벨 (메인 메뉴, 인게임 맵 등)
* **Scripts**: 게임 로직 C# 스크립트
* **Sounds**: 배경음 및 효과음
* **Texture**: 텍스처 및 머티리얼 에셋

## 주의 사항
* **대용량 파일**: 100MB 이상의 파일은 가급적 지양하거나, 반드시 Git LFS 추적 대상인지 확인하세요.
* **빌드 파일**: `Build.zip`과 같은 빌드 결과물은 GitHub에 직접 올리지 않고 `Releases` 기능을 활용해 공유하세요.
