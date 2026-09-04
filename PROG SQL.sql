CREATE DATABASE RaceDay;
GO

USE RaceDay;
GO

--User Table

CREATE TABLE [User] (
user_id INT IDENTITY(1,1) PRIMARY KEY,
first_name VARCHAR(50) NOT NULL,
last_name VARCHAR(50) NOT NULL,
email VARCHAR(100) NOT NULL UNIQUE,
password_hash VARCHAR(255) NOT NULL,
role VARCHAR(20) NOT NULL DEFAULT 'PARTICIPANT',
created_at DATETIME NOT NULL DEFAULT GETDATE(),

CONSTRAINT CK_User_Role
 CHECK (role IN ('ORGANISER', 'PARTICIPANT'))
 );

 GO

 --Event Table

 CREATE TABLE Event (
 event_id INT IDENTITY(1,1) PRIMARY KEY,
 organiser_id INT NOT NULL,
 name VARCHAR(100) NOT NULL,
 description VARCHAR(500) NOT NULL,
 event_date DATE NOT NULL,
 location VARCHAR(150) NOT NULL,
 distance DECIMAL(6,2) NOT NULL,
 event_type VARCHAR(20) NOT NULL,

 CONSTRAINT FK_Event_Organiser
   FOREIGN KEY (organiser_id)
   REFERENCES [User](user_id),

 CONSTRAINT CK_Event_Distance
     Check (distance > 0),

 Constraint CK_Event_Type
    Check (event_type IN ('RUN', 'WALK', 'CYCLE'))

);

GO

--Category Table

CREATE TABLE Category
(
    category_id INT IDENTITY(1,1) PRIMARY KEY,
    event_id INT NOT NULL,
    name VARCHAR(50) NOT NULL,
    category_type VARCHAR(20) NOT NULL,
    value VARCHAR(50) NOT NULL,

    CONSTRAINT FK_Category_Event
        FOREIGN KEY (event_id)
        REFERENCES Event(event_id),

    CONSTRAINT CK_Category_Type
        CHECK (category_type IN ('AGE', 'DISTANCE')),

    CONSTRAINT UQ_Category_Event_Name
        UNIQUE (event_id, name)
);
GO

--Route Table

CREATE TABLE Route
(
    route_id INT IDENTITY(1,1) PRIMARY KEY,
    event_id INT NOT NULL UNIQUE,
    distance_km DECIMAL(6,2) NOT NULL,
    start_location VARCHAR(150) NOT NULL,
    finish_location VARCHAR(150) NOT NULL,
    route_data VARCHAR(1000),

    CONSTRAINT FK_Route_Event
        FOREIGN KEY (event_id)
        REFERENCES Event(event_id),

    CONSTRAINT CK_Route_Distance
        CHECK (distance_km > 0)
);
GO

--Event Enrollment Table

CREATE TABLE Event_Enrollment
(
    enrollment_id INT IDENTITY(1,1) PRIMARY KEY,
    event_id INT NOT NULL,
    participant_id INT NOT NULL,
    category_id INT NOT NULL,
    registration_date DATETIME NOT NULL DEFAULT GETDATE(),
    status VARCHAR(20) NOT NULL DEFAULT 'REGISTERED',

    CONSTRAINT FK_Enrollment_Event
        FOREIGN KEY (event_id)
        REFERENCES Event(event_id),

    CONSTRAINT FK_Enrollment_Participant
        FOREIGN KEY (participant_id)
        REFERENCES [User](user_id),

    CONSTRAINT FK_Enrollment_Category
        FOREIGN KEY (category_id)
        REFERENCES Category(category_id),

    CONSTRAINT CK_Enrollment_Status
        CHECK (status IN ('REGISTERED', 'CANCELLED', 'COMPLETED')),

    CONSTRAINT UQ_Enrollment_Participant_Event
        UNIQUE (event_id, participant_id)
);
GO

--Result Table

CREATE TABLE Result
(
    result_id INT IDENTITY(1,1) PRIMARY KEY,
    enrollment_id INT NOT NULL UNIQUE,
    finish_time TIME NOT NULL,
    finish_position INT NOT NULL,

    CONSTRAINT FK_Result_Enrollment
        FOREIGN KEY (enrollment_id)
        REFERENCES Event_Enrollment(enrollment_id),

    CONSTRAINT CK_Result_Position
        CHECK (finish_position > 0)
);
GO

--Sample data
--USErs 2 organisers + 2 participants

INSERT INTO [User]
(
    first_name,
    last_name,
    email,
    password_hash,
    role
)
VALUES
(
    'Thabo',
    'Mokoena',
    'thabo@raceday.co.za',
    'hashed_password_1',
    'ORGANISER'
),
(
    'Lerato',
    'Nkosi',
    'lerato@raceday.co.za',
    'hashed_password_2',
    'ORGANISER'
),
(
    'Katlego',
    'Mathibela',
    'katlego@email.com',
    'hashed_password_3',
    'PARTICIPANT'
),
(
    'Sipho',
    'Dlamini',
    'sipho@email.com',
    'hashed_password_4',
    'PARTICIPANT'
);
GO


--Events
--3 Events

INSERT INTO Event
(
    organiser_id,
    name,
    description,
    event_date,
    location,
    distance,
    event_type
)
VALUES
(
    1,
    'Johannesburg City Run',
    'A road running event through Johannesburg.',
    '2026-10-10',
    'Johannesburg, Gauteng',
    10.00,
    'RUN'
),
(
    1,
    'Pretoria Charity Walk',
    'A community charity walking event.',
    '2026-10-24',
    'Pretoria, Gauteng',
    5.00,
    'WALK'
),
(
    2,
    'Cape Town Cycle Challenge',
    'A cycling event along scenic Cape Town routes.',
    '2026-11-07',
    'Cape Town, Western Cape',
    21.00,
    'CYCLE'
);
GO

-- CATEGORIES
-- Categories for EACH event

-- Johannesburg City Run
INSERT INTO Category
(
    event_id,
    name,
    category_type,
    value
)
VALUES
(
    1,
    'Under 20',
    'AGE',
    'Under 20'
),
(
    1,
    'Senior',
    'AGE',
    '20+'
),
(
    1,
    '10km',
    'DISTANCE',
    '10km'
);


-- Pretoria Charity Walk
INSERT INTO Category
(
    event_id,
    name,
    category_type,
    value
)
VALUES
(
    2,
    'Under 20',
    'AGE',
    'Under 20'
),
(
    2,
    'Senior',
    'AGE',
    '20+'
),
(
    2,
    '5km',
    'DISTANCE',
    '5km'
);


-- Cape Town Cycle Challenge
INSERT INTO Category
(
    event_id,
    name,
    category_type,
    value
)
VALUES
(
    3,
    'Under 20',
    'AGE',
    'Under 20'
),
(
    3,
    'Senior',
    'AGE',
    '20+'
),
(
    3,
    '21km',
    'DISTANCE',
    '21km'
);
GO


-- ROUTES
-- One route for each event

INSERT INTO Route
(
    event_id,
    distance_km,
    start_location,
    finish_location,
    route_data
)
VALUES
(
    1,
    10.00,
    'Johannesburg City Centre',
    'Ellis Park',
    'Johannesburg 10km road route'
),
(
    2,
    5.00,
    'Pretoria CBD',
    'Union Buildings',
    'Pretoria 5km charity walk route'
),
(
    3,
    21.00,
    'Cape Town Stadium',
    'Camps Bay',
    'Cape Town 21km cycling route'
);
GO



-- EVENT ENROLLMENTS
INSERT INTO Event_Enrollment
(
    event_id,
    participant_id,
    category_id,
    status
)
VALUES
(
    1,
    3,
    2,
    'REGISTERED'
),
(
    1,
    4,
    1,
    'REGISTERED'
),
(
    2,
    3,
    5,
    'REGISTERED'
),
(
    3,
    4,
    8,
    'REGISTERED'
);
GO


-- RESULTS
INSERT INTO Result
(
    enrollment_id,
    finish_time,
    finish_position
)
VALUES
(
    1,
    '01:02:35',
    15
),
(
    2,
    '01:15:20',
    32
);
GO


--Test Queries
-- View all users
SELECT *
FROM [User];


-- View all events
SELECT *
FROM Event;


-- View categories for each event
SELECT
    e.name AS Event_Name,
    c.name AS Category_Name,
    c.category_type,
    c.value
FROM Event e
INNER JOIN Category c
    ON e.event_id = c.event_id;


-- View enrolments
SELECT
    u.first_name + ' ' + u.last_name AS Participant,
    e.name AS Event_Name,
    c.name AS Category,
    ee.status
FROM Event_Enrollment ee
INNER JOIN [User] u
    ON ee.participant_id = u.user_id
INNER JOIN Event e
    ON ee.event_id = e.event_id
INNER JOIN Category c
    ON ee.category_id = c.category_id;


-- View participant results
SELECT
    u.first_name + ' ' + u.last_name AS Participant,
    e.name AS Event_Name,
    r.finish_time,
    r.finish_position
FROM Result r
INNER JOIN Event_Enrollment ee
    ON r.enrollment_id = ee.enrollment_id
INNER JOIN [User] u
    ON ee.participant_id = u.user_id
INNER JOIN Event e
    ON ee.event_id = e.event_id;
GO
