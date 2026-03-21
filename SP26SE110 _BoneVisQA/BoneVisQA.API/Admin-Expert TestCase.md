                                                                
                                                                Expert Role

api/Expert/cases

{
  "title": "Distal Radius Fracture - X-ray Case",
  "description": "A 45-year-old female presents with wrist pain after falling on an outstretched hand. Evaluate the X-ray image and identify the fracture characteristics.",
  "difficulty": "Easy",
  "categoryId": "8cae8008-bec5-4fb0-9808-becece8243a9",
  "suggestedDiagnosis": "Distal radius fracture (Colles fracture)",
  "keyFindings": "Dorsal displacement of distal radius fragment, cortical disruption, wrist swelling"
}

api/Expert/images

{
  "caseId": "69e859de-75d6-4cd3-9b19-8e6f340d8f00",
  "imageUrl": "",
  "modality": "MRI"
}

api/Expert/annotations

{
    "id": "9f4e2ba3-69ed-49a7-9a54-47c1083330e5",
    "label": "Distal radius fracture line",
    "coordinates": "{\"x\":120,\"y\":240,\"width\":80,\"height\":60}"
  }
}


api/Expert/case-tag

{
  "medicalCaseId": "69e859de-75d6-4cd3-9b19-8e6f340d8f00",
  "tagId": "0cbcc68b-43db-48cb-b4df-68e9119df435"
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
  "caseId": "69e859de-75d6-4cd3-9b19-8e6f340d8f00",
  "questionText": "Which radiographic feature best suggests a Colles fracture in this case?",
  "type": "MultipleChoice",
  "optionA": "Dorsal displacement of distal radius fragment",
  "optionB": "Medial displacement of ulna",
  "optionC": "Joint space narrowing",
  "optionD": "Periosteal reaction",
  "correctAnswer": "A"
}

/api/Expert/class/assign

"classId":"0bf619ce-62dc-4787-9429-2862df0d4ac0",
"quizId": "5c05995a-c6b4-49fd-bcf1-df63fc20e5e4",



api/Expert/attempts/score

"attemptId": "0ca4b8a0-8f35-4447-9567-cb81b0e39e00",

//================================================================================================================

                                                           ADMIN ROLE

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


