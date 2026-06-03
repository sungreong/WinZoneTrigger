---
FRAME: None
---

# WinZoneTrigger 소개: 장소에 도착하면 PC가 먼저 준비되는 Windows 앱

PC를 켤 때마다 같은 준비를 반복할 때가 있습니다.

집에서는 노트 앱과 개발 도구를 열고, 회사에서는 업무 문서와 협업 도구를 띄우고, 작업실에서는 특정 Wi-Fi에 연결한 뒤 필요한 링크를 브라우저에 올려둡니다. 하나하나는 작은 일이지만 매일 반복하면 꽤 번거롭습니다.

**WinZoneTrigger**는 이런 반복을 줄이기 위해 만든 Windows 트레이 앱입니다.

Windows 위치 좌표나 주변 Wi-Fi를 감지해서, 사용자가 특정 장소에 들어왔을 때 미리 정해둔 동작을 자동으로 실행합니다. 앱 실행, Chrome 링크 열기, 소리 설정 변경, 명령어 실행을 "장소 진입"이라는 신호 하나로 묶는 도구입니다.

## 핵심 요약

- GitHub 저장소: [https://github.com/sungreong/WinZoneTrigger](https://github.com/sungreong/WinZoneTrigger)
- 사용 대상: Windows PC
- 주요 기능: 위치 좌표 또는 주변 Wi-Fi 감지 후 앱, 링크, 소리 설정, 명령어 자동 실행
- 라이선스: MIT License
- 개인정보: 광고, 분석, 원격 추적, 텔레메트리 없음
- 설정 저장 위치: `%APPDATA%\WinZoneTrigger\config.json`
- 설치 파일: [GitHub Releases](https://github.com/sungreong/WinZoneTrigger/releases/latest)에서 `WinZoneTrigger_Setup.exe` 다운로드

![WinZoneTrigger main screen](assets/screenshot-main.png)

## 어디서 받을 수 있나

WinZoneTrigger는 GitHub 저장소와 Releases에서 확인할 수 있습니다.

[GitHub에서 WinZoneTrigger 보기](https://github.com/sungreong/WinZoneTrigger)

[WinZoneTrigger_Setup.exe 받기](https://github.com/sungreong/WinZoneTrigger/releases/latest)

설치하려면 Releases에 올라간 `WinZoneTrigger_Setup.exe`를 내려받아 실행하면 됩니다. 현재 사용자 계정에 앱이 설치되고, 시작 메뉴 바로가기, Windows 시작 시 자동 실행, 프로그램 제거 항목이 등록됩니다.

## 어떤 문제를 해결하나

장소가 바뀌면 PC에서 하는 일도 바뀝니다.

집에서 필요한 앱과 회사에서 필요한 앱은 다릅니다. 작업실에서 여는 링크와 조용한 공간에서 필요한 소리 설정도 다릅니다. 그런데 Windows PC는 보통 사용자가 매번 직접 준비해야 합니다.

WinZoneTrigger는 이 준비 과정을 자동화합니다.

예를 들어 이런 식으로 사용할 수 있습니다.

- 집 Wi-Fi가 보이면 Obsidian, Docker Desktop, 개인 프로젝트 링크를 자동으로 열기
- 회사 Wi-Fi가 보이면 업무 문서, 협업 도구, 사내 시스템 링크를 자동으로 열기
- 특정 장소에 들어가면 음소거를 켜거나 끄기
- 작업실에 도착하면 필요한 앱과 명령어를 순서대로 실행하기

핵심은 "PC가 내가 있는 장소를 보고 작업 시작 상태를 맞춰준다"는 점입니다.

## 아이디어의 출발점

이 프로그램의 아이디어는 삼성 핸드폰에 있는 **Modes and Routines**에서 출발했습니다.

스마트폰에서는 특정 장소에 도착하거나 특정 Wi-Fi에 연결됐을 때 루틴을 실행할 수 있습니다. 집에 도착하면 무음 모드를 끄고, 회사에 도착하면 알림 방식을 바꾸고, 운전 중에는 필요한 기능만 켜는 식입니다.

WinZoneTrigger는 이 감각을 Windows PC로 옮긴 프로그램입니다.

모바일 루틴처럼 조건과 행동을 연결하되, 대상은 스마트폰이 아니라 데스크톱 작업 환경입니다. "내가 이 장소에 도착했을 때 PC가 먼저 무엇을 준비해주면 좋을까?"라는 질문에서 출발했습니다.

## 주요 기능

WinZoneTrigger는 위치별 규칙을 만들고, 그 위치에 들어왔을 때 실행할 동작을 지정하는 방식으로 동작합니다.

- Windows 위치 서비스의 위도/경도 좌표로 장소 감지
- 주변 Wi-Fi SSID를 기준으로 장소 감지
- 좌표 감지와 Wi-Fi 감지 동시 사용
- 위치별 `운영 중` / `미운영` 전환
- 위치 진입 시 특정 Wi-Fi 연결 시도
- Chrome 링크 여러 개 자동 열기
- 시작 메뉴 앱, 실행 파일, 바로가기, 앱 프로토콜 실행
- 고급 명령어를 한 줄씩 실행
- 음소거 또는 음소거 해제
- Windows 시작 시 트레이 앱으로 자동 실행
- 상태/로그 탭에서 조건 일치와 실행 기록 확인

실외나 넓은 범위는 좌표 기반 감지가 어울리고, 집이나 회사처럼 특정 Wi-Fi가 보이는 실내 공간은 Wi-Fi 기반 감지가 실용적입니다.

## 반복 실행을 피하는 방식

자동화 도구에서 은근히 중요한 부분은 "언제 실행하느냐"입니다.

WinZoneTrigger는 같은 위치 안에 계속 머무는 동안 스캔할 때마다 동작을 반복 실행하지 않습니다. 밖에 있다가 해당 위치 안으로 들어온 순간에만 실행합니다.

그래서 브라우저 탭이 계속 늘어나거나, 앱이 반복해서 실행되는 불편을 줄일 수 있습니다.

## 테스트하고 운영하기

위치 자동화는 잘못 설정하면 오히려 귀찮아질 수 있습니다.

그래서 WinZoneTrigger에는 운영 전에 확인할 수 있는 흐름을 넣었습니다.

- `테스트해보기`: 현재 PC 상태가 선택한 위치 조건과 맞는지만 확인
- `동작 테스트`: 설정된 실행 동작을 지금 한 번 실행
- `운영하기`: 해당 위치를 실제 자동 실행 대상으로 켜기
- `운영 중지`: 해당 위치를 자동 실행 대상에서 제외

좌표와 Wi-Fi 조건이 제대로 잡히는지 먼저 확인하고, 실행 동작까지 따로 테스트한 뒤 운영할 수 있습니다.

## 개인정보와 데이터 수집

WinZoneTrigger는 사용자의 데이터를 외부 서버로 가져가거나 전송하지 않습니다.

위치 규칙, 좌표, Wi-Fi SSID, 링크, 앱 실행 목록은 로컬 설정 파일에만 저장됩니다.

```text
%APPDATA%\WinZoneTrigger\config.json
```

앱에는 광고, 분석, 원격 추적, 텔레메트리 기능이 없습니다. Windows 위치 정보와 Wi-Fi 목록은 위치 조건 확인을 위해 현재 PC에서만 읽습니다.

네트워크 요청은 사용자가 등록한 Chrome 링크를 열거나, 사용자가 등록한 앱과 명령어가 자체적으로 수행하는 동작에 한정됩니다.

## 라이선스

WinZoneTrigger는 **MIT License**로 공개되어 있습니다.

소스 코드와 라이선스 내용은 GitHub 저장소와 프로젝트의 `LICENSE` 파일에서 확인할 수 있습니다.

[GitHub 저장소에서 라이선스 확인하기](https://github.com/sungreong/WinZoneTrigger)

## 비슷한 제품과의 차이

비슷한 아이디어를 가진 제품은 여러 가지가 있습니다.

Samsung Modes and Routines, Apple Shortcuts, IFTTT, Tasker는 위치나 조건을 기준으로 동작을 실행하는 경험을 제공합니다. 특히 Samsung Modes and Routines는 이 프로젝트의 아이디어에 가장 가까운 출발점입니다. 다만 이들은 주로 모바일 기기나 클라우드 서비스 중심입니다.

반대로 Power Automate Desktop, AutoHotkey, PowerToys Workspaces는 Windows에서 강력한 자동화 수단을 제공합니다. 하지만 WinZoneTrigger는 범용 자동화 도구가 아니라, **장소 기반으로 PC 작업 환경을 준비하는 것**에 집중합니다.

정리하면 WinZoneTrigger는 이 중간에 있습니다.

- 모바일 루틴처럼 장소와 행동을 직관적으로 연결
- Windows PC에서 실제로 여는 앱, 링크, 명령어를 자동 실행
- 복잡한 스크립트보다 위치 규칙 중심의 UI 제공
- 로컬 설정 파일 중심으로 동작

## 이런 사람에게 어울립니다

- 집, 회사, 작업실처럼 PC를 쓰는 장소가 여러 곳인 사람
- 장소마다 여는 앱과 링크가 반복되는 사람
- 삼성 루틴 같은 조건 기반 자동화를 Windows에서도 쓰고 싶은 사람
- 복잡한 스크립트보다 UI로 자동화 규칙을 관리하고 싶은 사람
- 로컬 중심의 작은 Windows 생산성 도구를 선호하는 사람

## 마무리

WinZoneTrigger는 거창한 자동화 플랫폼이라기보다, 일상적인 PC 준비 과정을 줄이기 위한 작고 구체적인 도구입니다.

집에 도착하면 개인 작업 환경을, 회사 Wi-Fi가 보이면 업무 환경을, 조용한 공간에 들어가면 소리 설정을 자동으로 맞춥니다. 삼성 핸드폰의 루틴에서 얻은 위치 기반 자동화의 감각을 Windows 데스크톱에 맞게 옮긴 프로그램입니다.

프로그램은 아래 GitHub 저장소에서 확인할 수 있습니다.

[https://github.com/sungreong/WinZoneTrigger](https://github.com/sungreong/WinZoneTrigger)

## 참고 링크

- [GitHub - WinZoneTrigger](https://github.com/sungreong/WinZoneTrigger)
- [Samsung - Use Modes and Routines on your Galaxy phone or tablet](https://www.samsung.com/us/support/answer/ANS10002538/)
- [Apple - Intro to personal automation in Shortcuts](https://support.apple.com/guide/shortcuts/intro-to-personal-automation-apd690170742/ios)
- [IFTTT - Location integrations](https://ifttt.com/location)
- [Tasker - Location Context](https://tasker.joaoapps.com/userguide/en/loccontext.html)
- [Microsoft - Get started with Power Automate in Windows 11](https://learn.microsoft.com/en-us/power-automate/desktop-flows/getting-started-windows-11)
- [Microsoft - PowerToys Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces)
- [AutoHotkey](https://ahkscript.github.io/)
