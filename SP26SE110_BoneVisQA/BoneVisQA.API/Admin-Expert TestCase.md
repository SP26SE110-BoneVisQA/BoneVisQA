                                                                
                                                                Expert Role

api/Expert/cases

{
  "title": "Distal Radius Fracture - MRI Case",
  "description": "A 55-year-old female presents with wrist pain after falling on an outstretched hand. Evaluate the X-ray image and identify the fracture characteristics.",
  "difficulty": "Easy",
  "categoryId": "8cae8008-bec5-4fb0-9808-becece8243a9",
  "suggestedDiagnosis": "Distal radius fracture (Colles fracture)",
  "keyFindings": "Dorsal displacement of distal radius fragment, cortical disruption, wrist swelling"
}

api/Expert/images

{
  "caseId": "0425180d-e043-4dae-b07e-5cd2a837b006",
  "imageUrl": "",
  "modality": "MRI"
}

api/Expert/annotations

{
    "id": "d5e9b485-7dd0-4014-946c-69817c5823c1",
    "label": "Distal radius fracture line",
    "coordinates": "{\"x\":120,\"y\":240,\"width\":80,\"height\":60}"
  }
}


api/Expert/case-tag

{
  "medicalCaseId": "0425180d-e043-4dae-b07e-5cd2a837b006",
  "tagId": "b8727057-d8eb-421c-9636-f43dff762fed"
}

api/Expert/quizzes

{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Distal Radius Fracture Identification Quiz",
  "openTime": "2026-03-21T06:25:35.980Z",
  "closeTime": "2026-03-22T06:25:35.980Z",
  "timeLimit": 10,
  "passingScore": 6,
  "createdAt": "2026-03-21T06:25:35.980Z"
}

api/Expert/quizzes/questions

{
  "quizId": "5c05995a-c6b4-49fd-bcf1-df63fc20e5e4",
  "caseId": "0425180d-e043-4dae-b07e-5cd2a837b006",
  "questionText": "Which radiographic feature best suggests a Colles fracture in this case?",
  "type": "MultipleChoice",
  "optionA": "Dorsal displacement of distal radius fragment",
  "optionB": "Medial displacement of ulna",
  "optionC": "Joint space narrowing",
  "optionD": "Periosteal reaction",
  "correctAnswer": "A"
}

/api/Expert/class/assign

"classId":"1f28fc56-1308-48e8-a971-680d06bbeaab",
"quizId": "5c05995a-c6b4-49fd-bcf1-df63fc20e5e4",



api/Expert/attempts/score

"attemptId": "45f7669f-fa6d-48b8-8ab2-d5643831e026",

api/Expert/studentsubmit

{
  "studentId": "",
  "attemptId": "445f7669f-fa6d-48b8-8ab2-d5643831e026",
  "questionId": "5b9b5ef3-0cec-46da-8e66-f0d3fc34ffed",
  "studentAnswer": "a"
}

//================================================================================================================

                                                           ADMIN ROLE
api/admin/activate
"id":"3c97d301-b0ff-4d40-abe8-27c1408fffbf",


api/admin/deactivate
"id":"3c97d301-b0ff-4d40-abe8-27c1408fffbf",


api/admin/id/assign-role
"id":"3c97d301-b0ff-4d40-abe8-27c1408fffbf",
"role":"Student/Admin/Lecturter/Expert",


api/admin/id/revoke-role
"id":"3c97d301-b0ff-4d40-abe8-27c1408fffbf",
"role":"Student/Lecturter/Expert",


api/admin/tags
"documentid": "a44e4ff5-40d5-40f2-ba85-c861a66b9a96",
"tagid":      "0cbcc68b-43db-48cb-b4df-68e9119df435"

api/categoty/categoryid
"documentid": "a44e4ff5-40d5-40f2-ba85-c861a66b9a96",
"categoryId": "8cae8008-bec5-4fb0-9808-becece8243a9",


api/admin/id/version
"documentid": "a44e4ff5-40d5-40f2-ba85-c861a66b9a96",

api/admin/id/outdate
"documentid": "a44e4ff5-40d5-40f2-ba85-c861a66b9a96",


