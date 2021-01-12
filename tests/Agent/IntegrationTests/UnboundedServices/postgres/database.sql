--
-- PostgreSQL database dump
--

-- Dumped from database version 10.4
-- Dumped by pg_dump version 10.4

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: DATABASE postgres; Type: COMMENT; Schema: -; Owner: postgres
--

COMMENT ON DATABASE postgres IS 'default administrative connection database';


--
-- Name: newrelic; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA newrelic;


ALTER SCHEMA newrelic OWNER TO postgres;

--
-- Name: plpgsql; Type: EXTENSION; Schema: -; Owner: 
--

CREATE EXTENSION IF NOT EXISTS plpgsql WITH SCHEMA pg_catalog;


--
-- Name: EXTENSION plpgsql; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION plpgsql IS 'PL/pgSQL procedural language';


--
-- Name: adminpack; Type: EXTENSION; Schema: -; Owner: 
--

CREATE EXTENSION IF NOT EXISTS adminpack WITH SCHEMA pg_catalog;


--
-- Name: EXTENSION adminpack; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION adminpack IS 'administrative functions for PostgreSQL';


SET default_tablespace = '';

SET default_with_oids = false;

--
-- Name: teammembers; Type: TABLE; Schema: newrelic; Owner: postgres
--

CREATE TABLE newrelic.teammembers (
    id integer NOT NULL,
    firstname character varying(45),
    lastname character varying(45),
    email character varying(45),
    phone character varying(45),
    twitter character varying(45)
);


ALTER TABLE newrelic.teammembers OWNER TO postgres;

--
-- Name: teammembers_id_seq; Type: SEQUENCE; Schema: newrelic; Owner: postgres
--

CREATE SEQUENCE newrelic.teammembers_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER TABLE newrelic.teammembers_id_seq OWNER TO postgres;

--
-- Name: teammembers_id_seq; Type: SEQUENCE OWNED BY; Schema: newrelic; Owner: postgres
--

ALTER SEQUENCE newrelic.teammembers_id_seq OWNED BY newrelic.teammembers.id;


--
-- Name: teammembers id; Type: DEFAULT; Schema: newrelic; Owner: postgres
--

ALTER TABLE ONLY newrelic.teammembers ALTER COLUMN id SET DEFAULT nextval('newrelic.teammembers_id_seq'::regclass);


--
-- Data for Name: teammembers; Type: TABLE DATA; Schema: newrelic; Owner: postgres
--

COPY newrelic.teammembers (id, firstname, lastname, email, phone, twitter) FROM stdin;
1	Matthew	Furter	mfurter@bogus.com	800-555-9090	@mfurter
2	Nick	Raff	nraff@bogus.com	888-555-9999	@nraff
3	Bob	Majors	bmajors@bogus.com	800-567-7777	@bmajors
4	Micah	Weiss	mweiss@bogus.com	888-345-2121	@mweiss
5	Kirby	Hapschatt	khapschatt@bogus.com	800-767-5340	@khapschatt
\.


--
-- Name: teammembers_id_seq; Type: SEQUENCE SET; Schema: newrelic; Owner: postgres
--

SELECT pg_catalog.setval('newrelic.teammembers_id_seq', 5, true);


--
-- Name: teammembers teammembers_pkey; Type: CONSTRAINT; Schema: newrelic; Owner: postgres
--

ALTER TABLE ONLY newrelic.teammembers
    ADD CONSTRAINT teammembers_pkey PRIMARY KEY (id);


--
-- PostgreSQL database dump complete
--

