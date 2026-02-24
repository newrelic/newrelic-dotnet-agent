// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

//! Tests verifying that all COM interface GUIDs match corprof.h.
//! A wrong GUID means QueryInterface will fail and the profiler won't work.

use com::Interface;

#[test]
fn profiler_clsid_netfx_matches_cpp() {
    use newrelic_profiler_poc::profiler_callback::CLSID_PROFILER_NETFX;

    // Must match C++ profiler exactly: {71DA0A04-7777-4EC6-9643-7D28B46A8A41}
    assert_eq!(CLSID_PROFILER_NETFX.data1, 0x71DA0A04);
    assert_eq!(CLSID_PROFILER_NETFX.data2, 0x7777);
    assert_eq!(CLSID_PROFILER_NETFX.data3, 0x4EC6);
    assert_eq!(
        CLSID_PROFILER_NETFX.data4,
        [0x96, 0x43, 0x7D, 0x28, 0xB4, 0x6A, 0x8A, 0x41]
    );
}

#[test]
fn profiler_clsid_coreclr_matches_cpp() {
    use newrelic_profiler_poc::profiler_callback::CLSID_PROFILER_CORECLR;

    // Must match C++ profiler exactly: {36032161-FFC0-4B61-B559-F6C5D41BAE5A}
    assert_eq!(CLSID_PROFILER_CORECLR.data1, 0x36032161);
    assert_eq!(CLSID_PROFILER_CORECLR.data2, 0xFFC0);
    assert_eq!(CLSID_PROFILER_CORECLR.data3, 0x4B61);
    assert_eq!(
        CLSID_PROFILER_CORECLR.data4,
        [0xB5, 0x59, 0xF6, 0xC5, 0xD4, 0x1B, 0xAE, 0x5A]
    );
}

#[test]
fn icor_profiler_callback_guids_match_corprof_h() {
    use newrelic_profiler_poc::interfaces::*;

    // ICorProfilerCallback: {176FBED1-A55C-4796-98CA-A9DA0EF883E7}
    let iid = ICorProfilerCallback::IID;
    assert_eq!(iid.data1, 0x176FBED1);
    assert_eq!(iid.data2, 0xA55C);
    assert_eq!(iid.data3, 0x4796);

    // ICorProfilerCallback2: {8A8CC829-CCF2-49FE-BBAE-0F022228071A}
    let iid = ICorProfilerCallback2::IID;
    assert_eq!(iid.data1, 0x8A8CC829);
    assert_eq!(iid.data2, 0xCCF2);

    // ICorProfilerCallback3: {4FD2ED52-7731-4B8D-9469-03D2CC3086C5}
    let iid = ICorProfilerCallback3::IID;
    assert_eq!(iid.data1, 0x4FD2ED52);
    assert_eq!(iid.data2, 0x7731);

    // ICorProfilerCallback4: {7B63B2E3-107D-4D48-B2F6-F61E229470D2}
    let iid = ICorProfilerCallback4::IID;
    assert_eq!(iid.data1, 0x7B63B2E3);
    assert_eq!(iid.data2, 0x107D);
}

#[test]
fn icor_profiler_info_guids_match_corprof_h() {
    use newrelic_profiler_poc::profiler_info::*;

    // ICorProfilerInfo: {28B5557D-3F3F-48b4-90B2-5F9EEA2F6C48}
    let iid = ICorProfilerInfo::IID;
    assert_eq!(iid.data1, 0x28B5557D);
    assert_eq!(iid.data2, 0x3F3F);

    // ICorProfilerInfo2: {CC0935CD-A518-487d-B0BB-A93214E65478}
    let iid = ICorProfilerInfo2::IID;
    assert_eq!(iid.data1, 0xCC0935CD);
    assert_eq!(iid.data2, 0xA518);

    // ICorProfilerInfo3: {B555ED4F-452A-4E54-8B39-B5360BAD32A0}
    let iid = ICorProfilerInfo3::IID;
    assert_eq!(iid.data1, 0xB555ED4F);
    assert_eq!(iid.data2, 0x452A);

    // ICorProfilerInfo4: {0D8FDCAA-6257-47BF-B1BF-94DAC88466EE}
    let iid = ICorProfilerInfo4::IID;
    assert_eq!(iid.data1, 0x0D8FDCAA);
    assert_eq!(iid.data2, 0x6257);

    // ICorProfilerInfo5: {07602928-CE38-4B83-81E7-74ADAF781214}
    let iid = ICorProfilerInfo5::IID;
    assert_eq!(iid.data1, 0x07602928);
    assert_eq!(iid.data2, 0xCE38);
}
