# AlpoLib Data

## Table Data
- 데이터 구조를 기능별로 미리 선언한다. (record 형)
- Excel 또는 Google Sheet 를 이용하여 테이블 데이터를 만든다.
- Serializer 를 통해 바이너리 파일로 변환한다.
  - Menu / AlpoLib / Data / Serializer / Generate Serializer
- 런타임에서는 바이너리 파일을 스레드에서 동시에 읽어낸다.

## User Data
- 기능별로 클래스를 나누어서 선언한다.
- Util의 GameState를 이용하여 로컬에 저장한다.
- IUserDataSaveProcess를 이용하여 ApplicationPauseLister.OnSaveEvent 에서 자동으로 저장을 수행하게 할 수 있다.
