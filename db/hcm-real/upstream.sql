-- Entrada verbatim del usuario (2026-10-20): forma real de las tablas del HCM.
-- ESTE FICHERO NO SE EJECUTA. Es la fuente de verdad del shape (SPEC-012 §3):
-- cuando el shape cambie, reemplazar este archivo por la nueva versión del
-- usuario y adaptar schema.sql + vistas + seed en la misma revisión.
-- Notas de lectura: el paste usa sintaxis MySQL con backticks; la línea de la
-- columna de job title aparece con comillas simples ('job:title'), artefacto
-- previsible del pegado; las cláusulas CHARACTER SET/COLLATE no aplican en
-- PostgreSQL. La adaptación ejecutable vive en schema.sql.

CREATE TABLE `employee` (
  `id` int NOT NULL AUTO_INCREMENT,
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `deleted_on` datetime DEFAULT NULL,
  `first_email` varchar(75) NOT NULL DEFAULT '',
  `phone` varchar(20)NOT NULL DEFAULT '',
  `birthday_date` datetime DEFAULT NULL,
  `hire_date` datetime DEFAULT NULL,
  `name` varchar(75),
  `department` varchar(75),
  `last_name` varchar(75) DEFAULT '',
  `last_login` datetime DEFAULT NULL,
  `language_id` int DEFAULT NULL,
  `role_id` int NOT NULL DEFAULT '0',
  `status_id` tinyint NOT NULL DEFAULT '2',
  'job:title' varchar(75),
  `updated_on` datetime DEFAULT NULL,
  `vacancy_supervisor` int NOT NULL DEFAULT '0',
  `timecheck_supervisor` int NOT NULL DEFAULT '0',
  `photo_url` varchar(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `second_email` varchar(75) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
)


CREATE TABLE `timeoff_type` (
  `id` int NOT NULL AUTO_INCREMENT,
  `amount` int NOT NULL DEFAULT '0',
  `date_start` datetime DEFAULT NULL,
  `date_end` datetime DEFAULT NULL,
  `name` varchar(100),
  `status_id` tinyint NOT NULL DEFAULT '1',
  `fiscal_year` int NOT NULL DEFAULT '0',
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
)

CREATE TABLE `timeoff` (
  `id` int NOT NULL AUTO_INCREMENT,
  `date_start` datetime NOT NULL,
  `date_end` datetime DEFAULT NULL,
  `note` varchar(1000),
  `status_id` tinyint NOT NULL DEFAULT '1',
  `timeoff_type_id` int NOT NULL,
  `employee_id` int NOT NULL,
  `employee_id_supervisor` int DEFAULT NULL,
  `date_supervised` datetime DEFAULT NULL,
  `supervisor_note` mediumtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin,
  `fiscal_year` int DEFAULT NULL,
  `by_hour` bit(1) DEFAULT b'0',
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
)

CREATE TABLE `working_day` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `status_id` int NOT NULL,
  `name` varchar(50) ,
  `description` varchar(200),
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `modified_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `enable_delays` tinyint DEFAULT '0',
  `web_allowed` bit(1) DEFAULT b'1',
  `mobile_allowed` bit(1) DEFAULT b'1',
  `is_default` bit(1) NOT NULL DEFAULT b'0',
  PRIMARY KEY (`id`),
)

CREATE TABLE `working_day_assigment` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `employee_id` int NOT NULL,
  `working_day_id` bigint NOT NULL,
  `status_id` int NOT NULL,
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `modified_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),

)

CREATE TABLE `time_check` (
  `id` int NOT NULL AUTO_INCREMENT,
  `employee_id` int NOT NULL,
  `working_day_id` bigint NOT NULL,
  `check_type_id` tinyint NOT NULL,
  `checkout_type_id` int NOT NULL,
  `time_check_date_utc` datetime NOT NULL,
  `time_check_date_long` bigint NOT NULL,
  `time_check_timezone` varchar(64) DEFAULT NULL,
  `check_device_type_id` tinyint NOT NULL,
  `status_id` tinyint NOT NULL,
  `supervisor_status_date` datetime NOT NULL,
  `employee_note` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `longitude` float(10,6) NOT NULL,
  `latitude` float(10,6) NOT NULL,
  `ip_address` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `created_on` datetime NOT NULL,
  `modified_on` datetime NOT NULL,
  `delay_id` int NOT NULL DEFAULT '0',
  `photo_face_url` varchar(255) DEFAULT NULL,
  `photo_face_status_id` tinyint DEFAULT '0',
  PRIMARY KEY (`id`),
)

CREATE TABLE `time_check_delay` (
  `id` int NOT NULL AUTO_INCREMENT,
  `comments` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `status_id` tinyint NOT NULL,
  `created_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `modified_on` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `approved_by` int NOT NULL,
  `employee_id` int NOT NULL,
  PRIMARY KEY (`id`),

);

-- Semántica aportada por el usuario (verbatim, 2026-10-20):
-- check_type_id = 1 para entrada, dos para salida
-- checkout_type_id = 1 para pausa, 2 pasa almuerzo, 3 para finalización de jornada.
-- de igual manera la forma de saber si un usuario no ha comenzado jornada es
-- viendo que no tenga registros en ese día
-- Vocabulario de status (verbatim, 2026-10-20): 1 pendiente, 2 activo, 3 eliminado.
