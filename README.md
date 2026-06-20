# TodoWpfPortable

Windows 11용 무설치 WPF To-Do 앱입니다.

## 기능

- 로컬 JSON 파일 저장만 사용
- 네트워크/통신 기능 없음
- 항상 위 표시 기본 활성화
- 할 일 추가, 완료, 삭제
- 변경 후 자동저장
- JSON 백업 생성 및 복원
- 다크 모드 기반의 심플한 UI

## 데이터 위치

앱 데이터는 아래 경로에 저장됩니다.

```text
%LOCALAPPDATA%\TodoWpfPortable\tasks.json
```

백업 파일은 아래 경로에 생성됩니다.

```text
%LOCALAPPDATA%\TodoWpfPortable\Backups
```

## 단일 실행파일 배포

.NET 8 SDK가 설치된 Windows 환경에서 실행합니다.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

생성 파일:

```text
bin\Release\net8.0-windows\win-x64\publish\TodoWpfPortable.exe
```
