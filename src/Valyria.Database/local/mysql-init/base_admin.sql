-- Runs once, when the local MySQL container first creates its data volume.
-- The views and procedures are created with DEFINER=`base_admin`, which has to exist for them to be used
-- (CI and the integration tests do the same).
CREATE USER IF NOT EXISTS 'base_admin'@'%' IDENTIFIED BY 'unused';
GRANT ALL ON *.* TO 'base_admin'@'%';
