# AlpoLib Data

## Table Data
- 데이터 구조를 record 형태로 미리 선언한다.
- Excel 또는 Google Sheet 를 이용하여 테이블 데이터를 만든다.
- Serializer 를 통해 바이너리 파일로 변환한다.
  - Menu / AlpoLib / Data / Serializer / Generate Serializer
- 런타임에서는 바이너리 파일을 스레드에서 동시에 읽어낸다.

Management Project 의 TableLoadScene 참고
- 테이블 데이터 선언
  - Sample/Scripts/Data/Table
- 엑셀 파일
  - Sample/TableData/Excel
